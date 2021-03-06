﻿namespace Fredis
#nowarn "864"

open System
open System.Collections.Generic
open System.Collections.Concurrent
open System.Threading
open System.Diagnostics
open System.Threading
open System.Threading.Tasks

open BookSleeve

// removed pool for factory. something fishy going on with pool and blocking requests, 
// it is just unneeded complexity, let's keep + operator for "create a new IDisposable copy 
// of this connection" that will kill itself on Dispose()
// Come back here when pool is **really** needed. For blocking queues an agent will take a connection
// for its whole lifetime, so the pool is not needed, just a new connection.

// TODO make Clone logic similar to BS SubscriberChannel with Interlocked.Exchange. This will 
// allow to use +conn it a tight loop like in the test without opening a new connection on each loop. 

// TODO Async looping lock based on setnx. Do not try to use blpop - too complex for artificial gain and uses a connection per lock.


[<AutoOpenAttribute>]
module Utils =
    /// Option-coalescing operator
    let inline (??=) (opt:'a option) (fb:'a) = if opt.IsSome then opt.Value else fb


type Connection
    (host:string,?port:int,?ioTimeout:int,?password:string,?maxUnsent:int,?allowAdmin:bool,?syncTimeout:int) as this =
    inherit RedisConnection(host,port??=6379,ioTimeout??=(-1),password??=null,maxUnsent??=Int32.MaxValue,allowAdmin??=false)
    
    let factory : ConnectionFactory ref = ref (new ConnectionFactory(host,port??=6379,ioTimeout??=(-1),password??=null,maxUnsent??=Int32.MaxValue,allowAdmin??=false))
    
    do 
        base.Open().Wait() // |> ignore

    // BookSleeve recommends using the same connection for simple operations since they are thread safe
    // However some connections could be blocking (e.g. using blocking locks) or slow (e.g. complex lua scripts)

    /// Get a new connection to the same server with the same parameters
    /// Use this method when a call to Redis could be blocking, e.g. when using distributed locks
    member this.Clone with get() = (!factory).Get()

    member this.Dispose() = base.Dispose()

    /// Shortcut for Use() method
    static member (~+) (connection:Connection) = connection.Clone

    interface IDisposable with
        member this.Dispose() = this.Dispose()


and [<Sealed;AllowNullLiteralAttribute>] internal ConnectionFactory
    (host:string,?port:int,?ioTimeout:int,?password:string,?maxUnsent:int,?allowAdmin:bool,?syncTimeout:int,?minPoolSize:int,?maxPoolSize:int) =
    
    let port = defaultArg port 6379
    let ioTimeout = defaultArg ioTimeout -1
    let password = defaultArg password null
    let maxUnsent = defaultArg maxUnsent Int32.MaxValue
    let allowAdmin = defaultArg allowAdmin false
    let syncTimeout = defaultArg syncTimeout 10000
    let minPoolSize = defaultArg minPoolSize 1
    let maxPoolSize = defaultArg maxPoolSize 10
    let pool = new BlockingCollection<Connection>(maxPoolSize)
    
    member internal this.Get() : Connection = 
        new Connection(host,port,ioTimeout,password,maxUnsent,allowAdmin,syncTimeout)

[<AutoOpenAttribute>]
module ConnectionModule =
    /// Get an existing connection from a pool or a new connection to the same server with same parameters
    /// Use this method when a call to Redis could be blocking, e.g. when using distributed locks
    let (~+) (conn:Connection) = conn.Clone
    /// GetOpenSubscriberChannel on connection
    let (~%) (conn:Connection) = conn.GetOpenSubscriberChannel()
    /// Async await plain Task and return Async<unit>, to be used with do! inside Async
    let (!~)  (t: IAsyncResult) = t |> (Async.AwaitIAsyncResult >> Async.Ignore)
    /// Async await typed Task<'T> and return Async<'T>, to be used with let! inside Async
    let inline (!!)  (t: Task<'T>) = t |> Async.AwaitTask
    /// Run plain Task/IAsyncResult on current thread
    let (!~!)  (t: IAsyncResult) = t |> (Async.AwaitIAsyncResult >> Async.Ignore >> Async.RunSynchronously)
    /// Run task Task<'T> on current thread and return results
    let inline (!!!)  (t: Task<'T>) = t.Result // |> (Async.AwaitTask >> Async.RunSynchronously)
    