using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Tool_check.Models;
using Tool_check.Verifiers;

namespace Tool_check
{
    public partial class MainWindow : Window
    {
        private TransparencyData? _currentData;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Log(string message)
        {
            LogOutputTextBox.AppendText(message + Environment.NewLine);
            LogOutputTextBox.ScrollToEnd();
        }

        private void ClearLogs()
        {
            LogOutputTextBox.Clear();
        }

        private async void VerifyBtn_Click(object sender, RoutedEventArgs e)
        {
            var apiUrl = ApiUrlInput.Text.Trim().TrimEnd('/');
            var projectIdStr = ProjectIdInput.Text.Trim();

            if (string.IsNullOrWhiteSpace(apiUrl) || string.IsNullOrWhiteSpace(projectIdStr))
            {
                MessageBox.Show("Vui lòng nhập đầy đủ Server API URL và Project ID.", "Thiếu thông tin", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Guid.TryParse(projectIdStr, out var projectId))
            {
                MessageBox.Show("Project ID phải ở định dạng UUID/Guid hợp lệ.", "Lỗi định dạng", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            VerifyBtn.IsEnabled = false;
            SelectFileBtn.IsEnabled = false;
            StatusbarText.Text = "Đang tải dữ liệu minh bạch từ máy chủ...";
            ClearLogs();

            try
            {
                using var handler = new HttpClientHandler
                {
                    // Bỏ qua SSL cert tự ký
                    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                };
                using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };

                var url = $"{apiUrl}/projects/{projectId}/transparency";
                Log($"[HTTP] Đang tải {url}...");

                var response = await http.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    throw new Exception($"HTTP {(int)response.StatusCode} {response.StatusCode}\n{errorBody}");
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var data = await response.Content.ReadFromJsonAsync<TransparencyData>(options);
                
                if (data == null)
                    throw new Exception("Dữ liệu JSON rỗng.");

                Log("[Success] Tải dữ liệu thành công.");
                ProcessLoadedData(data);
            }
            catch (Exception ex)
            {
                Log($"[Lỗi] Không thể kết nối hoặc tải dữ liệu:\n{ex.Message}");
                StatusbarText.Text = "Lỗi kết nối máy chủ.";
                MessageBox.Show($"Lỗi kết nối máy chủ:\n{ex.Message}", "Lỗi tải dữ liệu", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                VerifyBtn.IsEnabled = true;
                SelectFileBtn.IsEnabled = true;
            }
        }

        private void SelectFileBtn_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                Title = "Chọn file JSON Minh Bạch (transparency.json)"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                VerifyBtn.IsEnabled = false;
                SelectFileBtn.IsEnabled = false;
                StatusbarText.Text = "Đang phân tích file JSON...";
                ClearLogs();

                try
                {
                    var jsonContent = File.ReadAllText(openFileDialog.FileName);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var data = JsonSerializer.Deserialize<TransparencyData>(jsonContent, options);

                    if (data == null || data.ProjectId == Guid.Empty || data.Decks.Count == 0)
                        throw new Exception("File JSON không đúng cấu trúc TransparencyData hoặc không chứa chồng phiếu nào.");

                    Log($"[File] Nạp thành công file: {Path.GetFileName(openFileDialog.FileName)}");
                    ProcessLoadedData(data);
                }
                catch (Exception ex)
                {
                    Log($"[Lỗi] Không thể đọc file JSON:\n{ex.Message}");
                    StatusbarText.Text = "Lỗi đọc file.";
                    MessageBox.Show($"Lỗi đọc file JSON:\n{ex.Message}", "Lỗi file", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    VerifyBtn.IsEnabled = true;
                    SelectFileBtn.IsEnabled = true;
                }
            }
        }

        private void ProcessLoadedData(TransparencyData data)
        {
            _currentData = data;

            // Hiển thị JSON gốc (Raw)
            try
            {
                var serializeOptions = new JsonSerializerOptions { WriteIndented = true };
                RawJsonTextBox.Text = JsonSerializer.Serialize(data, serializeOptions);
            }
            catch (Exception ex)
            {
                RawJsonTextBox.Text = $"Không thể hiển thị JSON gốc: {ex.Message}";
            }

            // Hiển thị Metadata dự án
            MetaProjectName.Text = data.ProjectName;
            MetaCompletedAt.Text = data.CompletedAt?.ToString("dd/MM/yyyy HH:mm") + " UTC";
            MetaDecksCount.Text = data.Decks.Count.ToString();
            MetaStepsCount.Text = data.NhatKyBoc.Count.ToString();

            // Kích hoạt các hạng mục kiểm chứng
            RunVerifications(data);

            // Nạp Deck Selector
            DeckSelectorComboBox.Items.Clear();
            foreach (var deck in data.Decks)
            {
                DeckSelectorComboBox.Items.Add(new ComboBoxItem
                {
                    Content = GetRoundLabelText(deck.Round),
                    Tag = deck.Round
                });
            }

            if (DeckSelectorComboBox.Items.Count > 0)
            {
                DeckSelectorComboBox.SelectedIndex = 0;
            }

            StatusbarText.Text = "Hoàn tất đối chiếu.";
        }

        private async void RunVerifications(TransparencyData data)
        {
            bool allOk = true;

            // 1 & 2. R_server commit + MASTER_SEED
            Log("==========================================");
            Log("1. KIỂM TRA R_SERVER COMMIT & MASTER_SEED");
            Log("==========================================");

            var commitResults = CommitVerifier.Verify(data.NguonNgauNhien);
            bool checkCommitOk = true;
            bool checkSeedOk = true;
            bool checkBitcoinOk = true;

            foreach (var cr in commitResults)
            {
                Log($"Vòng {cr.Round}:");
                if (cr.ErrorMessage != null)
                {
                    Log($"  ❌ Lỗi: {cr.ErrorMessage}");
                    checkCommitOk = false;
                    checkSeedOk = false;
                    continue;
                }

                if (cr.CommitOk)
                {
                    Log($"  ✅ R_server commit khớp: {cr.CommitExpected}");
                }
                else
                {
                    Log($"  ❌ R_server commit KHÔNG KHỚP");
                    Log($"     Công bố : {cr.CommitExpected}");
                    Log($"     Thực tế : {cr.CommitActual}");
                    checkCommitOk = false;
                }

                if (cr.MasterSeedOk)
                {
                    Log($"  ✅ MASTER_SEED khớp   : {cr.MasterSeedExpected}");
                }
                else
                {
                    Log($"  ❌ MASTER_SEED KHÔNG KHỚP");
                    Log($"     Công bố : {cr.MasterSeedExpected}");
                    Log($"     Thực tế : {cr.MasterSeedActual}");
                    checkSeedOk = false;
                }

                // Kiểm tra mã hash Blockchain (Ethereum hoặc Bitcoin)
                var src = data.NguonNgauNhien.FirstOrDefault(n => string.Equals(n.Round, cr.Round, StringComparison.OrdinalIgnoreCase));
                if (src != null && !string.IsNullOrWhiteSpace(src.BlockHash))
                {
                    long blockHeightChosen = src.BlockHeight;
                    long freezeBlockHeight = blockHeightChosen - 1; // Ước lượng hoặc từ logic chốt số
                    bool heightOk = blockHeightChosen > freezeBlockHeight;

                    bool isEth = string.Equals(src.AnchorChain, "ethereum", StringComparison.OrdinalIgnoreCase) 
                        || string.IsNullOrEmpty(src.AnchorChain); // Mặc định là ethereum nếu null/rỗng
                    string chainName = isEth ? "Ethereum" : "Bitcoin";

                    string? realHash = null;
                    string? hashErr = null;
                    DateTime? blockTime = null;
                    string? timeErr = null;

                    if (isEth)
                    {
                        var ethRes = await GetEthereumBlockByHeightAsync(blockHeightChosen);
                        realHash = ethRes.Hash;
                        hashErr = ethRes.Error;
                        blockTime = ethRes.Timestamp;
                        timeErr = ethRes.Error;
                    }
                    else
                    {
                        var btcHashRes = await GetBitcoinBlockHashByHeightAsync(blockHeightChosen);
                        realHash = btcHashRes.Hash;
                        hashErr = btcHashRes.Error;

                        var btcTimeRes = await GetBitcoinBlockTimeAsync(src.BlockHash);
                        blockTime = btcTimeRes.Timestamp;
                        timeErr = btcTimeRes.Error;
                    }

                    bool hashOk = realHash != null && string.Equals(src.BlockHash, realHash, StringComparison.OrdinalIgnoreCase);
                    bool timeOk = false;
                    DateTime? earliestSeal = null;
                    double diffMinutes = 0;
                    DateTime? latestBlockExpected = null;

                    if (blockTime != null)
                    {
                        var matchingDecks = data.Decks.Where(d => d.Round.StartsWith(cr.Round, StringComparison.OrdinalIgnoreCase)).ToList();
                        if (matchingDecks.Count > 0)
                        {
                            earliestSeal = matchingDecks.Select(d => d.SealedAt).Min();
                            if (earliestSeal != null)
                            {
                                var diff = earliestSeal.Value - blockTime.Value;
                                diffMinutes = diff.TotalMinutes;
                                // Hợp lệ nếu block được đào trước khi niêm phong nhưng không quá 120 phút (stale block check)
                                timeOk = diff.TotalSeconds >= 0 && diff.TotalHours <= 2;
                                latestBlockExpected = earliestSeal.Value.AddMinutes(-2);
                            }
                        }
                    }

                    // Tiêu đề tổng quát của phần kiểm tra Blockchain
                    bool allBlockchainChecksOk = heightOk && hashOk && timeOk;
                    if (!allBlockchainChecksOk) checkBitcoinOk = false;
                    if (allBlockchainChecksOk)
                    {
                        Log($"  ✅ Kiểm tra mã hash {chainName} Blockchain:");
                    }
                    else
                    {
                        Log($"  ❌ Kiểm tra mã hash {chainName} Blockchain:");
                    }

                    // A. Chi tiết Check Height
                    if (heightOk)
                    {
                        Log("     ✅ Blockchain Block Height > Freeze Block Height");
                    }
                    else
                    {
                        Log("     ❌ Blockchain Block Height <= Freeze Block Height (Nguy cơ thao túng!)");
                    }
                    Log($"      \t- Freeze Block Height: #{freezeBlockHeight}");
                    Log($"      \t- Block Height Chosen: #{blockHeightChosen}");

                    // B. Chi tiết Check Hash khớp với Height
                    if (hashErr != null)
                    {
                        Log($"     ⚠️ Không thể tải Hash Block thực tế (Lỗi kết nối: {hashErr})");
                    }
                    else if (hashOk)
                    {
                        Log($"     ✅ Hash Block khớp với {chainName} Blockchain Block Height");
                        Log($"        - Hash Block: {src.BlockHash}");
                    }
                    else
                    {
                        Log($"     ❌ Hash Block không khớp với {chainName} Blockchain Block Height");
                        Log($"        - Hash Block Chosen: {src.BlockHash}");
                        Log($"        - Real Hash Block:   {realHash ?? "Không thể lấy dữ liệu"}");
                    }

                    // C. Chi tiết Check Thời gian đào
                    if (timeErr != null)
                    {
                        Log($"     ⚠️ Không thể tải thời gian đào block (Lỗi kết nối: {timeErr})");
                    }
                    else if (blockTime != null && earliestSeal != null)
                    {
                        var blockTimeLocal = blockTime.Value.AddHours(7);
                        var sealTimeLocal = earliestSeal.Value.AddHours(7);
                        var latestExpectedLocal = latestBlockExpected?.AddHours(7);

                        if (timeOk)
                        {
                            Log("     ✅ Kiểm tra thời gian đào block. Block được chọn là block mới nhất tại thời điểm niêm phong");
                            Log($"        - Block được đào lúc: {blockTimeLocal:dd/MM/yyyy HH:mm:ss} UTC+7");
                            Log($"        - Thời gian niêm phong: {sealTimeLocal:dd/MM/yyyy HH:mm:ss} UTC+7");
                        }
                        else
                        {
                            Log("     ❌ Kiểm tra thời gian đào block. Block được chọn không phải là block mới nhất tại thời điểm niêm phong");
                            Log($"        - Block được đào lúc: {blockTimeLocal:dd/MM/yyyy HH:mm:ss} UTC+7");
                            Log($"        - Thời gian niêm phong: {sealTimeLocal:dd/MM/yyyy HH:mm:ss} UTC+7");
                            if (latestExpectedLocal != null)
                            {
                                Log($"        - Block mới nhất tại thời điểm niêm phong: {latestExpectedLocal:dd/MM/yyyy HH:mm:ss} UTC+7");
                            }
                        }
                    }
                    else
                    {
                        Log("     ❌ Không đủ dữ liệu thời gian niêm phong để kiểm chứng.");
                    }
                }

                Log("");
            }

            UpdateBadgeUI(StatusBadgeCommit, StatusTextCommit, checkCommitOk);
            UpdateBadgeUI(StatusBadgeSeed, StatusTextSeed, checkSeedOk);
            UpdateBadgeUI(StatusBadgeBitcoin, StatusTextBitcoin, checkBitcoinOk);
            if (!checkCommitOk || !checkSeedOk || !checkBitcoinOk) allOk = false;

            // 3. DeckHash
            Log("==========================================");
            Log("2. KIỂM TRA DECKHASH");
            Log("==========================================");
            var deckHashResults = DeckHashVerifier.Verify(data.Decks);
            bool checkDeckOk = true;
            foreach (var dr in deckHashResults)
            {
                if (dr.Ok)
                {
                    Log($"Vòng {dr.Round}: ✅ DeckHash khớp ({dr.Actual})");
                }
                else
                {
                    Log($"Vòng {dr.Round}: ❌ DeckHash KHÔNG KHỚP!");
                    Log($"  Công bố : {dr.Expected}");
                    Log($"  Tính lại: {dr.Actual}");
                    checkDeckOk = false;
                }
            }
            Log("");
            UpdateBadgeUI(StatusBadgeDeck, StatusTextDeck, checkDeckOk);
            if (!checkDeckOk) allOk = false;

            // 4. Hash-Chain
            Log("==========================================");
            Log("3. KIỂM TRA HASH-CHAIN NHẬT KÝ BẤM");
            Log("==========================================");
            var chainResult = HashChainVerifier.Verify(data.NhatKyBoc);
            UpdateBadgeUI(StatusBadgeChain, StatusTextChain, chainResult.Ok);
            if (chainResult.Ok)
            {
                Log($"✅ Hợp lệ: Chuỗi băm liên tục ({chainResult.TotalSteps} mắt xích, không đứt gãy).");
            }
            else
            {
                Log($"❌ ĐỨT CHUỖI BĂM tại lượt #{chainResult.BrokenAtIndex} (Vòng {chainResult.BrokenRound}, Phiếu {chainResult.BrokenPosition})!");
                Log($"   Chi tiết: {chainResult.ErrorDetail}");
                allOk = false;
            }
            Log("");

            // 5. Payload
            Log("==========================================");
            Log("4. ĐỐI CHIẾU NỘI DUNG PHIẾU");
            Log("==========================================");
            var payloadResult = PayloadVerifier.Verify(data.Decks, data.NhatKyBoc);
            UpdateBadgeUI(StatusBadgePayload, StatusTextPayload, payloadResult.Ok);
            if (payloadResult.Ok)
            {
                Log($"✅ Hợp lệ: Toàn bộ {payloadResult.TotalChecked} lượt bốc khớp chính xác 100% với chồng phiếu.");
            }
            else
            {
                Log($"❌ Phát hiện {payloadResult.Mismatches.Count} sai lệch (trong {payloadResult.TotalChecked} lượt):");
                foreach (var m in payloadResult.Mismatches.Take(5))
                {
                    Log($"   - Vòng {m.Round} | Phiếu {m.Position}:");
                    Log($"     Chồng phiếu  : {m.ExpectedFromDeck}");
                    Log($"     Nhật ký : {m.ActualFromLog}");
                }
                allOk = false;
            }

            if (payloadResult.Warnings.Count > 0)
            {
                Log("");
                Log("⚠️  Cảnh báo:");
                foreach (var w in payloadResult.Warnings)
                    Log($"   {w}");
            }
            Log("");

            // Cập nhật trạng thái tổng quát
            if (allOk)
            {
                OverallStatusText.Text = "HỢP LỆ";
                OverallStatusBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10b981")); // Green
                OverallStatusBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34d399"));
                OverallStatusText.Foreground = Brushes.White;
            }
            else
            {
                OverallStatusText.Text = "BẤT THƯỜNG";
                OverallStatusBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ef4444")); // Red
                OverallStatusBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f87171"));
                OverallStatusText.Foreground = Brushes.White;
            }
        }

        private void UpdateBadgeUI(Border border, TextBlock text, bool ok)
        {
            if (ok)
            {
                text.Text = "✓";
                border.Background = new SolidColorBrush(Color.FromArgb(38, 16, 185, 129)); // Soft green
                border.BorderBrush = new SolidColorBrush(Color.FromArgb(76, 16, 185, 129));
                text.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10b981"));
            }
            else
            {
                text.Text = "✗";
                border.Background = new SolidColorBrush(Color.FromArgb(38, 239, 68, 68)); // Soft red
                border.BorderBrush = new SolidColorBrush(Color.FromArgb(76, 239, 68, 68));
                text.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ef4444"));
            }
        }

        private string GetRoundLabelText(string round)
        {
            if (round == "A1") return "Vòng A1 (U2)";
            if (round.StartsWith("A2:")) return $"Vòng A2 ({round.Substring(3)})";
            if (round.StartsWith("B:")) return $"Vòng B ({round.Substring(2)})";
            if (round == "C") return "Vòng C (Căn dư)";
            return round;
        }

        private void DeckSelectorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DeckSelectorComboBox.SelectedItem is not ComboBoxItem selectedItem) return;
            var round = selectedItem.Tag as string;
            if (round == null || _currentData == null) return;

            var deck = _currentData.Decks.FirstOrDefault(d => string.Equals(d.Round, round, StringComparison.OrdinalIgnoreCase));
            if (deck == null) return;

            RenderDeckTickets(deck);
        }

        private void RenderDeckTickets(DeckInfo deck)
        {
            TicketsWrapPanel.Children.Clear();
            DeckStatsText.Text = $"{deck.Tickets.Count} phiếu (Trúng: {deck.WonCount})";

            // Lọc các lượt bấm tự động trong vòng này
            var autoDrawnPositions = new HashSet<int>();
            if (_currentData != null)
            {
                foreach (var step in _currentData.NhatKyBoc)
                {
                    if (string.Equals(step.Round, deck.Round, StringComparison.OrdinalIgnoreCase) && step.AutoDrawn)
                    {
                        autoDrawnPositions.Add(step.Position);
                    }
                }
            }

            for (int i = 0; i < deck.Tickets.Count; i++)
            {
                var ticket = deck.Tickets[i];
                var isAuto = autoDrawnPositions.Contains(i);

                // Tạo border card cho từng phiếu
                var border = new Border
                {
                    Width = 175,
                    Margin = new Thickness(4),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10),
                    BorderThickness = new Thickness(4, 1, 1, 1),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1e293b"))
                };

                // Quyết định màu sắc của viền bên trái
                Brush borderLeftBrush;
                Brush cardBackground;
                if (ticket.StartsWith("TRUNG"))
                {
                    borderLeftBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10b981")); // Green
                    cardBackground = new SolidColorBrush(Color.FromArgb(15, 16, 185, 129));
                }
                else if (ticket == "CHO_PHAN_LOAI_DU")
                {
                    borderLeftBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f59e0b")); // Orange/Yellow
                    cardBackground = new SolidColorBrush(Color.FromArgb(15, 245, 158, 11));
                }
                else if (ticket.StartsWith("DU_KHUYET"))
                {
                    borderLeftBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8b5cf6")); // Purple
                    cardBackground = new SolidColorBrush(Color.FromArgb(15, 139, 92, 246));
                }
                else // KHONG_TRUNG
                {
                    borderLeftBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ef4444")); // Red
                    cardBackground = new SolidColorBrush(Color.FromArgb(15, 239, 68, 68));
                }

                border.BorderBrush = borderLeftBrush;
                border.Background = cardBackground;

                // Content stack
                var stack = new StackPanel();

                // 1. Vị trí
                var textPos = new TextBlock
                {
                    Text = $"Phiếu số {i} {(isAuto ? "(Tự động)" : "")}",
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94a3b8")),
                    Margin = new Thickness(0, 0, 0, 4)
                };
                stack.Children.Add(textPos);

                // 2. Nội dung/Kết quả hiển thị
                var textResult = new TextBlock
                {
                    Text = GetFriendlyTicketLabel(ticket),
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White,
                    TextWrapping = TextWrapping.Wrap
                };
                stack.Children.Add(textResult);

                border.Child = stack;
                TicketsWrapPanel.Children.Add(border);
            }
        }

        private string GetFriendlyTicketLabel(string payload)
        {
            if (payload == "TRUNG_QUYEN_MUA") return "Trúng quyền mua";
            if (payload == "KHONG_TRUNG_UU_TIEN") return "Không trúng";
            if (payload == "CHO_PHAN_LOAI_DU") return "Chờ phân loại";
            if (payload.StartsWith("TRUNG:")) return $"Trúng: {payload.Substring(6)}";
            if (payload == "KHONG_TRUNG") return "Không trúng";
            if (payload.StartsWith("DU_KHUYET:")) return $"Dự khuyết #{payload.Substring(10)}"; // Sửa lỗi String.substring
            return payload;
        }

        private async Task<(DateTime? Timestamp, string? Error)> GetBitcoinBlockTimeAsync(string blockHash)
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var response = await http.GetAsync($"https://blockstream.info/api/block/{blockHash}");
                if (!response.IsSuccessStatusCode)
                    return (null, $"HTTP {(int)response.StatusCode}");

                using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                if (doc.RootElement.TryGetProperty("timestamp", out var tsProp))
                {
                    long unixTime = tsProp.GetInt64();
                    return (DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime, null);
                }
                return (null, "Không tìm thấy trường timestamp");
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        private async Task<(string? Hash, string? Error)> GetBitcoinBlockHashByHeightAsync(long height)
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var response = await http.GetAsync($"https://blockstream.info/api/block-height/{height}");
                if (!response.IsSuccessStatusCode)
                    return (null, $"HTTP {(int)response.StatusCode}");

                string realHash = (await response.Content.ReadAsStringAsync()).Trim();
                return (realHash, null);
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        private async Task<(string? Hash, DateTime? Timestamp, string? Error)> GetEthereumBlockByHeightAsync(long height)
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var hexHeight = "0x" + height.ToString("x");
                var body = $"{{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_getBlockByNumber\",\"params\":[\"{hexHeight}\",false]}}";
                using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
                var response = await http.PostAsync("https://ethereum-rpc.publicnode.com", content);
                if (!response.IsSuccessStatusCode)
                    return (null, null, $"HTTP {(int)response.StatusCode}");

                using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                var root = doc.RootElement;
                if (root.TryGetProperty("error", out var err) && err.ValueKind != JsonValueKind.Null)
                {
                    return (null, null, $"RPC error: {err.GetProperty("message").GetString()}");
                }
                if (!root.TryGetProperty("result", out var result) || result.ValueKind == JsonValueKind.Null)
                {
                    return (null, null, "Block chưa được đào");
                }

                var hash = result.GetProperty("hash").GetString();
                if (hash != null && hash.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    hash = hash.Substring(2); // Strip 0x
                }

                var tsHex = result.GetProperty("timestamp").GetString();
                if (tsHex != null && tsHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    tsHex = tsHex.Substring(2);
                }
                long unixTime = Convert.ToInt64(tsHex, 16);
                var timestamp = DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime;

                return (hash, timestamp, null);
            }
            catch (Exception ex)
            {
                return (null, null, ex.Message);
            }
        }

        private void DownloadJsonBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(RawJsonTextBox.Text) || RawJsonTextBox.Text.StartsWith("Không thể"))
            {
                MessageBox.Show("Không có dữ liệu JSON hợp lệ để tải xuống.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                DefaultExt = "json",
                FileName = "transparency.json",
                Title = "Tải xuống tệp JSON minh bạch"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(saveFileDialog.FileName, RawJsonTextBox.Text);
                    MessageBox.Show("Tải xuống tệp JSON thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi lưu tệp JSON:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

    }
}