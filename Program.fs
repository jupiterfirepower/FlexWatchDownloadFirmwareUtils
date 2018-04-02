// Learn more about F# at http://fsharp.net
// See the 'F# Tutorial' project for more help.

open System
open System.Net
open System.IO
open System.Text.RegularExpressions
open HtmlAgilityPack
open FSharpx.Extras
open FSharpx.Control
open FSharp.Control
open _3dEye.Funcs.Helpers

// ----------------------------------------------------------------------------
// Helper functions for downloading documents, extracting links etc.
let downloadFile( url: string, filename: string) = async {
  try let wc = new WebClient()
      let! file = wc.AsyncDownloadFile(Uri(url), filename)
      return Some file 
  with _ -> return None }

/// Asynchronously download the document and parse the HTML
let downloadDocument url = async {
  try let wc = new WebClient()
      let! html = wc.AsyncDownloadString(Uri(url))
      let doc = new HtmlDocument()
      doc.LoadHtml(html)
      return Some doc 
  with _ -> return None }

let getFilename (s:string) =
    let index = s.IndexOf("filename")
    let filename = s.Substring(index+9)
    filename

let extractLinksText (doc:HtmlDocument, startWith:string) = 
  try
    [| for a in doc.DocumentNode.SelectNodes("//a") do
        if a.Attributes.Contains("href") then
          let href = a.Attributes.["href"].Value
          if href.StartsWith(startWith) then 
            yield  a.InnerText |]
  with _ -> [||]

  /// Extract all links from the document that start with "http://"
let extractLinks (doc:HtmlDocument, urlRoot: string) = 
  try
    [ for a in doc.DocumentNode.SelectNodes("//a[@href]") do
        if a.Attributes.Contains("href") then
          let href = a.Attributes.["href"].Value
          //if href.StartsWith("javascript", StringComparison.InvariantCultureIgnoreCase) then do! continue;      // ignore javascript on buttons using a tags
          let urlNext = new Uri(href, UriKind.RelativeOrAbsolute);
           // Make it absolute if it's relative
          if not(urlNext.IsAbsoluteUri) then
             let urlNext = new Uri(new Uri(urlRoot), urlNext)
             yield urlNext.AbsoluteUri
          else
             yield urlNext.AbsoluteUri
          ]
  with _ -> []

  /// Extract the <title> of the web page
let getTitle (doc:HtmlDocument) =
  let title = doc.DocumentNode.SelectSingleNode("//title")
  if title <> null then title.InnerText.Trim() else "Untitled"

/// Crawl the internet starting from the specified page
/// From each page follow the first not-yet-visited page
let rec targetCrawler url ext = 
  let visited = new System.Collections.Generic.HashSet<_>()

  // Visits page and then recursively visits all referenced pages
  let rec loop(url: string) = asyncSeq {
    if visited.Add(url) then
      if url.EndsWith(ext) then
        //let! file = downloadFile(url, getFilename url) 
        //Console.WriteLine("File will be downloaded- {0}", url)
        yield url, "file" 
      else 
      let! doc = downloadDocument url
      //Console.WriteLine("randomCrawl - {0}", url)
      match doc with 
      | Some doc ->
          // Yield url and title as the next element
          yield url, getTitle doc
          // For every link, yield all referenced pages too
          for link in extractLinks(doc, url) do
            yield! loop link 
      | _ -> () }
  loop url

//let mutable index = 0

let downloadFirmware(page : int) = async { 
  let index = ref 0
  let! doc = downloadDocument(String.Format("http://www.flexwatch.com/board/list.asp?gotopage={0}&Category=Firmware", page))
  let links = extractLinks(doc.Value, "read_count")
  let linksText = extractLinksText(doc.Value, "read_count")
  for link in links do
  Console.WriteLine(!index)
  Console.WriteLine(link)
  let desc = linksText.[!index].Trim()
  Console.WriteLine(desc)
  index := !index + 1
  let! docCurrent = downloadDocument (sprintf "%s%s" "http://www.flexwatch.com/board/"  link)
  let extractedLinks = extractLinks( docCurrent.Value, "download.asp")
  for extractedLink in extractedLinks do
  let! file = downloadFile((sprintf "%s%s" "http://www.flexwatch.com/board/"  extractedLink), (sprintf "%s%s" desc (getFilename extractedLink))) 
  Console.WriteLine(getFilename extractedLink)
  ()
}

let CheckDir(dir : string) = async { 
  if not(Directory.Exists(dir)) then
       Directory.CreateDirectory(dir) |> ignore
  ()
}

[<EntryPoint>]
let main argv = 
    
    //let p =factorial 10


    let targetFileExt = ".zip"
    targetCrawler "http://www.flexwatch.com/board/list.asp?gotopage=1&Category=Firmware" targetFileExt
    |> AsyncSeq.filter (fun (url, title) -> url.EndsWith(targetFileExt))
    |> AsyncSeq.map fst
    //|> AsyncSeq.iter (printfn "%s")
    |> AsyncSeq.iter (fun url -> downloadFile(url, getFilename url) |> ignore)
    |> Async.Start

    //CheckDir("firmware") |> ignore
    //downloadFirmware(1) |> Async.StartImmediate
    //downloadFirmware(2) |> Async.StartImmediate

    //downloadFirmware(1) |> Async.StartAsTask |>  Async.AwaitTask |> ignore
    //downloadFirmware(2) |> Async.StartAsTask |>  Async.AwaitTask |> ignore
    Console.ReadLine() |> ignore
    0 // return an integer exit code
