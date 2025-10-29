// See https://aka.ms/new-console-template for more information
using H3Util;

Console.WriteLine("Hello, World!");
var predictor = new VesselPrediction();
predictor.LoadModels(@"model");

var path = predictor.PredictPath(22.291519161433914, 114.18139223171458, 70, 70, 5);
//var path = predictor.PredictPath(22.29717, 114.20196, 60, 70, 5);
foreach (var step in path)
{
    Console.WriteLine($"{step["h3"]}, {step["lat"]}, {step["lon"]} (prob={step["prob"]})");
}

var result = predictor.CheckAnomaly(22.291519161433914, 114.18139223171458, 22.291519161433914, 114.18139223171458, 70, 70);
Console.WriteLine($"{result["reason"]}, prob={result["prob"]}");
