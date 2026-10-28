namespace TVCleaner.Tests
{
    public static class TestData
    {
        public const string DbJson = @"{
  ""version"": 1,
  ""updated"": ""2026-10-28"",
  ""testedModels"": [""MODEL_A""],
  ""packages"": [
    { ""package"": ""ru.sberdevices.survey"", ""name"": ""Опросы"", ""description"": ""опросы"", ""category"": ""ads"", ""risk"": ""low"", ""breaks"": """", ""canUninstall"": true },
    { ""package"": ""com.android.printspooler"", ""name"": ""Печать"", ""description"": ""печать"", ""category"": ""junk"", ""risk"": ""low"", ""breaks"": """", ""canUninstall"": false },
    { ""package"": ""ru.sberdevices.music"", ""name"": ""Звук"", ""description"": ""музыка"", ""category"": ""optional"", ""risk"": ""low"", ""breaks"": """", ""canUninstall"": true },
    { ""package"": ""ru.sberdevices.starlauncher"", ""name"": ""Лаунчер Сбера"", ""description"": ""главный экран"", ""category"": ""optional"", ""risk"": ""medium"", ""breaks"": ""главный экран"", ""canUninstall"": false },
    { ""package"": ""com.android.shell"", ""name"": ""Shell"", ""description"": ""ошибочно помечен"", ""category"": ""junk"", ""risk"": ""low"", ""breaks"": """", ""canUninstall"": false },
    { ""package"": ""mediatek.factorymenu.ui"", ""name"": ""Заводское меню"", ""description"": """", ""category"": ""system"", ""risk"": ""high"", ""breaks"": """", ""canUninstall"": false }
  ]
}";
    }
}
