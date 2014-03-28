open Facebook
open System
open System.Diagnostics
open System.Net
open FSharp.Data
open FSharp.Data.JsonExtensions
open Microsoft.FSharp.Control.WebExtensions

type Section = 
    | Home
    | Inbox

type LoginParms = { 
    client_id: string; 
    client_secret: string; 
    redirect_uri: string; 
    response_type: string;
    scope: string }    

type AccessTokenParms = { 
    client_id: string; 
    client_secret: string; 
    redirect_uri: string; 
    code: string }

type GetParms = { 
    client_id: string; 
    client_secret: string }

let getCode parms (client : FacebookClient) = 
    async { 
        use listener = new HttpListener()
        listener.Prefixes.Add "http://*:81/"
        listener.Start()
        printfn "Iniciando sesión con FB... "
        let login = client.GetLoginUrl parms
        Process.Start(string login) |> ignore
        // blocks until login is received
        let! c = async { return listener.GetContext() } 
        try 
            let code = c.Request.QueryString.Get("code")
            printfn "\nCode received: %s" code
            c.Response.OutputStream.Close()
            listener.Stop() 
            printfn "Presione enter para continuar"
            Console.ReadLine() |> ignore
            return code
        with 
            | ex -> let desc = c.Request.QueryString.Get("error_description")
                    printfn "%s - Error: %s" desc (string ex)
                    return ""
    }

let get section (client : FacebookClient) = 
    let parms = { 
        client_id = client.AppId
        client_secret = client.AppSecret }  
    let path = match section with 
                | Home -> "/me/home" 
                | Inbox -> "/me/inbox?fields=to,comments.fields(message)"                
    let result = client.Get<JsonObject> (path, parms)    
    result.[0] :?> seq<obj> //data element only (page is ommited)

let print section data =
    for e in data do
        let json = JsonValue.Parse(string e)
        match section with
            | Home -> 
                let name = json?from?name.AsString()
                let eType = json.GetProperty("type").AsString()
                printfn "%s - %s" eType name
            | Inbox -> 
                let dest = (json?``to``)?data.AsArray()
                let mutable mTo = dest.[0]?name.AsString()
                if dest.Length > 1 then
                    mTo <- mTo + " -> " + dest.[1]?name.AsString()

                if json.TryGetProperty("comments") <> None  then
                    let msg = new System.Text.StringBuilder()
                    for c in json?comments?data.AsArray() do
                        msg.AppendLine(c?message.AsString()) |> ignore
                    msg.ToString() |> printfn "%s\n%s" mTo 
                else
                    printfn "%s" mTo     
                           
let initClient = 
    let client = new FacebookClient(
                    AppId = "{Use your AppId}", 
                    AppSecret = "{Use your AppSecret}") 
                                                                         
    let parms = { 
        client_id = client.AppId
        client_secret = client.AppSecret
        redirect_uri = "{User your AppCanvas url address}"
        response_type = "code"
        scope = "read_stream,read_mailbox" }

    let code = getCode parms client |> Async.RunSynchronously

    let parms = { 
        client_id = client.AppId
        client_secret = client.AppSecret
        redirect_uri = parms.redirect_uri
        code = code }

    let result = client.Get("oauth/access_token", parms) :?> JsonObject        
    
    match result.ContainsKey "access_token" with 
        | true -> client.AccessToken <- result.[0] :?> string
        | false -> ()
    
    client

let printGet section (client : FacebookClient) : FacebookClient =      
    let data = get section client
    print section data
    client

[<EntryPoint>]
let main argv =             
    initClient 
    |> printGet Section.Home
    |> printGet Section.Inbox
    |> ignore
            
    Console.ReadLine() 
    |> ignore    
    0 // return an integer exit code