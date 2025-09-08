using Microsoft.AspNetCore.Components.Forms;
using System.IO.Compression;
using System.Xml;
using System.Xml.Xsl;
using System.Text;
using Microsoft.JSInterop;

namespace EgovXml2Pdf.Services
{
    public class FileProcessingService : IFileProcessingService
    {
        private readonly IJSRuntime _jsRuntime;

        public FileProcessingService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task ProcessFilesAsync(IEnumerable<IBrowserFile> files, Action<int, string> progressCallback)
        {
            var totalFiles = files.Count();
            var processedFiles = 0;
            var resultZipDataList = new List<string>();

            progressCallback(0, "処理を開始しています...");

            foreach (var file in files)
            {
                try
                {
                    progressCallback(
                        (int)((double)processedFiles / totalFiles * 80),
                        $"ファイルを処理中: {file.Name}"
                    );

                    var resultZipData = await ProcessSingleFileAsync(file, progressCallback);
                    resultZipDataList.Add(resultZipData);
                    processedFiles++;
                }
                catch (Exception ex)
                {
                    throw new Exception($"ファイル {file.Name} の処理中にエラーが発生しました: {ex.Message}", ex);
                }
            }

            progressCallback(90, "結果ファイルをZIP化しています...");

            // 結果をZIP化してダウンロード
            await CreateAndDownloadResultZipAsync(resultZipDataList);

            progressCallback(100, "処理が完了しました");
        }

        private async Task<string> ProcessSingleFileAsync(IBrowserFile file, Action<int, string> progressCallback)
        {
            try
            {
                // ファイルをメモリに読み込み
                using var stream = file.OpenReadStream(maxAllowedSize: 100 * 1024 * 1024); // 100MB制限
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                // ZIPファイルを解凍（メモリ内）
                var extractedFiles = new Dictionary<string, byte[]>();
                using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (!string.IsNullOrEmpty(entry.Name))
                        {
                            using var entryStream = entry.Open();
                            using var entryMemoryStream = new MemoryStream();
                            await entryStream.CopyToAsync(entryMemoryStream);
                            extractedFiles[entry.FullName] = entryMemoryStream.ToArray();
                        }
                    }
                }

                // XMLとXSLファイルを検索
                var xmlFiles = extractedFiles.Where(f => f.Key.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)).ToList();
                var xslFiles = extractedFiles.Where(f => f.Key.EndsWith(".xsl", StringComparison.OrdinalIgnoreCase)).ToList();

                // デバッグ情報を出力
                Console.WriteLine($"=== ファイル解析結果 ===");
                Console.WriteLine($"XMLファイル数: {xmlFiles.Count}");
                foreach (var xml in xmlFiles)
                {
                    Console.WriteLine($"  XML: {xml.Key}");
                }
                Console.WriteLine($"XSLファイル数: {xslFiles.Count}");
                foreach (var xsl in xslFiles)
                {
                    Console.WriteLine($"  XSL: {xsl.Key}");
                }

                if (!xmlFiles.Any())
                {
                    throw new Exception("XMLファイルが見つかりません");
                }

                // 各XMLファイルを処理（HTMLのみ）
                var processedFiles = new Dictionary<string, byte[]>();
                foreach (var xmlFile in xmlFiles)
                {
                    var htmlData = await ProcessXmlFileAsync(xmlFile, xslFiles, extractedFiles);
                    if (htmlData != null)
                    {
                        var htmlFileName = Path.GetFileNameWithoutExtension(xmlFile.Key) + "_ge.html";
                        processedFiles[htmlFileName] = htmlData;
                    }
                }

                // 結果をZIP化
                var resultZipData = await CreateResultZipAsync(extractedFiles, processedFiles, file.Name);
                
                // 結果をBase64エンコードして返す
                return Convert.ToBase64String(resultZipData);
            }
            catch (Exception ex)
            {
                throw new Exception($"ファイル {file.Name} の処理中にエラーが発生しました: {ex.Message}", ex);
            }
        }

        private async Task<byte[]?> ProcessXmlFileAsync(KeyValuePair<string, byte[]> xmlFile, List<KeyValuePair<string, byte[]>> xslFiles, Dictionary<string, byte[]> allFiles)
        {
            try
            {
                // XMLファイルを読み込み
                var xmlDoc = new XmlDocument();
                
                // XMLファイルの内容をデバッグ出力
                var xmlContent = Encoding.UTF8.GetString(xmlFile.Value);
                Console.WriteLine($"XML File Info:");
                Console.WriteLine($"  File Size: {xmlFile.Value.Length} bytes");
                Console.WriteLine($"  Content Length: {xmlContent.Length} characters");
                Console.WriteLine($"  Content Preview: {xmlContent.Substring(0, Math.Min(200, xmlContent.Length))}");
                
                using (var xmlStream = new MemoryStream(xmlFile.Value))
                {
                    xmlDoc.Load(xmlStream);
                }

                // 対応するXSLファイルを検索
                var xmlFileName = Path.GetFileNameWithoutExtension(xmlFile.Key);
                Console.WriteLine($"\n=== XMLファイル処理: {xmlFile.Key} ===");
                Console.WriteLine($"XMLファイル名（拡張子なし）: {xmlFileName}");
                
                // XMLファイル内のXSL参照を確認
                var xslReference = ExtractXslReference(xmlContent);
                Console.WriteLine($"XML内のXSL参照: {xslReference}");
                
                var xslFile = default(KeyValuePair<string, byte[]>);
                
                // 1. まずXML内のXSL参照を確認
                if (!string.IsNullOrEmpty(xslReference))
                {
                    xslFile = xslFiles.FirstOrDefault(x => 
                        Path.GetFileName(x.Key).Equals(xslReference, StringComparison.OrdinalIgnoreCase) ||
                        x.Key.Contains(xslReference));
                    Console.WriteLine($"XSL参照による検索結果: {(xslFile.Equals(default(KeyValuePair<string, byte[]>)) ? "見つからない" : xslFile.Key)}");
                }
                
                // 2. XSL参照で見つからない場合は、ファイル名の一致で検索
                if (xslFile.Equals(default(KeyValuePair<string, byte[]>)))
                {
                    xslFile = xslFiles.FirstOrDefault(x => 
                        Path.GetFileNameWithoutExtension(x.Key).Equals(xmlFileName, StringComparison.OrdinalIgnoreCase));
                    Console.WriteLine($"ファイル名一致による検索結果: {(xslFile.Equals(default(KeyValuePair<string, byte[]>)) ? "見つからない" : xslFile.Key)}");
                }
                
                // 3. それでも見つからない場合は、デフォルトのXSLファイルを探す
                if (xslFile.Equals(default(KeyValuePair<string, byte[]>)))
                {
                    xslFile = xslFiles.FirstOrDefault();
                    Console.WriteLine($"デフォルトXSLファイル: {(xslFile.Equals(default(KeyValuePair<string, byte[]>)) ? "見つからない" : xslFile.Key)}");
                }

                if (xslFile.Equals(default(KeyValuePair<string, byte[]>)))
                {
                    throw new Exception($"XSLファイルが見つかりません: {xmlFileName}");
                }
                
                Console.WriteLine($"使用するXSLファイル: {xslFile.Key}");

                // XSL変換を実行
                var htmlContent = await TransformXmlToHtmlAsync(xmlDoc, xslFile, allFiles);
                
                // HTMLデータを作成
                var htmlData = Encoding.UTF8.GetBytes(htmlContent);

                return htmlData;
            }
            catch (Exception ex)
            {
                throw new Exception($"XMLファイル {xmlFile.Key} の処理中にエラーが発生しました: {ex.Message}", ex);
            }
        }

        private Task<string> TransformXmlToHtmlAsync(XmlDocument xmlDoc, KeyValuePair<string, byte[]> xslFile, Dictionary<string, byte[]> allFiles)
        {
            try
            {
                // XMLファイルの内容をデバッグ出力
                Console.WriteLine($"XML Document Info:");
                Console.WriteLine($"  DocumentElement: {xmlDoc.DocumentElement?.Name ?? "null"}");
                Console.WriteLine($"  ChildNodes Count: {xmlDoc.ChildNodes.Count}");
                Console.WriteLine($"  OuterXml Length: {xmlDoc.OuterXml?.Length ?? 0}");
                
                if (xmlDoc.DocumentElement == null)
                {
                    Console.WriteLine("XML Document Root Element is null!");
                    Console.WriteLine($"XML Content Preview: {xmlDoc.OuterXml?.Substring(0, Math.Min(200, xmlDoc.OuterXml?.Length ?? 0)) ?? "null"}");
                }
                // XSLファイルを読み込み
                var xslDoc = new XmlDocument();
                using (var xslStream = new MemoryStream(xslFile.Value))
                {
                    xslDoc.Load(xslStream);
                }

                // XSL変換を実行
                var transform = new XslCompiledTransform();
                using (var xslReader = new XmlNodeReader(xslDoc))
                {
                    transform.Load(xslReader);
                }

                var htmlBuilder = new StringBuilder();
                using (var writer = new StringWriter(htmlBuilder))
                using (var xmlReader = new XmlNodeReader(xmlDoc))
                {
                    transform.Transform(xmlReader, null, writer);
                }

                var htmlContent = htmlBuilder.ToString();

                // preタグをpタグに変更（HTMLの表示問題を解決）
                htmlContent = System.Text.RegularExpressions.Regex.Replace(
                    htmlContent, 
                    @"<pre\b[^>]*>", 
                    "<p>", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );
                htmlContent = System.Text.RegularExpressions.Regex.Replace(
                    htmlContent, 
                    @"</pre>", 
                    "</p>", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );

                return Task.FromResult(htmlContent);
            }
            catch (Exception ex)
            {
                throw new Exception($"XSL変換中にエラーが発生しました: {ex.Message}", ex);
            }
        }



        private async Task CreateAndDownloadResultZipAsync(List<string> resultZipDataList)
        {
            try
            {
                // 複数のZIPファイルを統合
                var combinedZipData = await CombineZipFilesAsync(resultZipDataList);
                
                // JavaScriptでダウンロードを実行
                var fileName = $"egov_converted_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
                await _jsRuntime.InvokeVoidAsync("downloadFile", Convert.ToBase64String(combinedZipData), fileName);
            }
            catch (Exception ex)
            {
                throw new Exception($"結果ファイルの作成中にエラーが発生しました: {ex.Message}", ex);
            }
        }

        private async Task<byte[]> CreateResultZipAsync(Dictionary<string, byte[]> originalFiles, Dictionary<string, byte[]> processedFiles, string originalFileName)
        {
            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                var dirName = Path.GetFileNameWithoutExtension(originalFileName);
                
                // 元のファイルを追加
                foreach (var file in originalFiles)
                {
                    var entryName = $"{dirName}/{file.Key}";
                    var entry = archive.CreateEntry(entryName);
                    using (var entryStream = entry.Open())
                    {
                        await entryStream.WriteAsync(file.Value);
                    }
                }
                
                // 処理済みPDFファイルを追加
                foreach (var file in processedFiles)
                {
                    var entryName = $"{dirName}/{file.Key}";
                    var entry = archive.CreateEntry(entryName);
                    using (var entryStream = entry.Open())
                    {
                        await entryStream.WriteAsync(file.Value);
                    }
                }
            }
            
            return memoryStream.ToArray();
        }

        private async Task<byte[]> CombineZipFilesAsync(List<string> zipDataList)
        {
            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                foreach (var zipData in zipDataList)
                {
                    var zipBytes = Convert.FromBase64String(zipData);
                    using (var zipStream = new MemoryStream(zipBytes))
                    using (var sourceArchive = new ZipArchive(zipStream, ZipArchiveMode.Read))
                    {
                        foreach (var entry in sourceArchive.Entries)
                        {
                            if (!string.IsNullOrEmpty(entry.Name))
                            {
                                var newEntry = archive.CreateEntry(entry.FullName);
                                using (var sourceStream = entry.Open())
                                using (var newEntryStream = newEntry.Open())
                                {
                                    await sourceStream.CopyToAsync(newEntryStream);
                                }
                            }
                        }
                    }
                }
            }
            
            return memoryStream.ToArray();
        }

        private string ExtractXslReference(string xmlContent)
        {
            try
            {
                Console.WriteLine($"XSL参照抽出開始 - XML内容の最初の500文字:");
                Console.WriteLine(xmlContent.Substring(0, Math.Min(500, xmlContent.Length)));
                
                // XML内のXSL参照を抽出（より柔軟なパターン）
                var patterns = new[]
                {
                    // 標準的なxml-stylesheet処理命令
                    @"<\?xml-stylesheet\s+[^>]*href\s*=\s*[""']([^""']+)[""'][^>]*\?>",
                    // より柔軟なパターン
                    @"xml-stylesheet\s+[^>]*href\s*=\s*[""']([^""']+)[""']",
                    // href属性のみ
                    @"href\s*=\s*[""']([^""']*\.xsl)[""']",
                    // ファイル名のみ
                    @"([^/\\]+\.xsl)"
                };
                
                foreach (var pattern in patterns)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(xmlContent, pattern, 
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    
                    if (match.Success)
                    {
                        var result = match.Groups[1].Value;
                        Console.WriteLine($"XSL参照を発見: {result} (パターン: {pattern})");
                        return result;
                    }
                }
                
                Console.WriteLine("XSL参照が見つかりませんでした");
                return string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"XSL参照抽出エラー: {ex.Message}");
                return string.Empty;
            }
        }

    }
}
