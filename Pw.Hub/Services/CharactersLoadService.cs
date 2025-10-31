using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pw.Hub.Models;
using Pw.Hub.Infrastructure;
using Pw.Hub.Tools;

namespace Pw.Hub.Services;

/// <summary>
/// Реализация сервиса загрузки списка серверов и персонажей аккаунта
/// через встроенный браузер личного кабинета. Показывает окно логов процесса,
/// выполняет навигацию и парсинг DOM с помощью JavaScript, сохраняет данные в БД.
/// </summary>
public class CharactersLoadService : ICharactersLoadService
{
    /// <inheritdoc />
    public async Task LoadForAccountAsync(Account account, Pages.AccountPage accountPage, Window owner)
    {
        if (account == null) throw new ArgumentNullException(nameof(account));
        if (accountPage == null) throw new ArgumentNullException(nameof(accountPage));
        var browser = accountPage.Browser;
        var accountManager = accountPage.AccountManager;

        // Лог-окно как в существующей реализации
        var log = new Windows.ScriptLogWindow("Загрузка персонажей", closeWhenEnd: true) { Owner = owner };

        async Task RunAsync()
        {
            try
            {
                log.AppendLog($"Переключаемся на аккаунт: {account.Name}");
                await accountManager.ChangeAccountAsync(account.Id);

                log.AppendLog("Переходим на страницу промо-предметов...");
                await browser.NavigateAsync("https://pwonline.ru/promo_items.php");

                // Подождать появления нужных селектов (примитивный таймаут можно расширить при необходимости)
                await Task.Delay(300);

                // Дожидаемся появления селектов на странице
                await browser.WaitForElementExistsAsync("#pw_promos_shards", 5000);
                await browser.WaitForElementExistsAsync("#pw_promos_chars", 5000);

                // Получаем сервера (шарды) — возвращаем JSON через JSON.stringify
                var jsGetShards = "(function(){var s=document.querySelector('#pw_promos_shards');if(!s)return '[]';return JSON.stringify(Array.from(s.options).map(o=>({value:o.value,text:o.text})));})()";
                var shardsJson = await browser.ExecuteScriptAsync(jsGetShards);
                var shards = string.IsNullOrWhiteSpace(shardsJson) ? new List<JsOption>() : JsonSerializer.Deserialize<List<JsOption>>(shardsJson) ?? new List<JsOption>();
                log.AppendLog($"Найдено серверов: {shards.Count}");

                var servers = new List<AccountServer>();
                foreach (var shard in shards)
                {
                    if (string.IsNullOrWhiteSpace(shard?.value)) continue;
                    // Для каждого шарда получаем персонажей (устанавливаем значение и инициируем change)
                    var jsChars = "(function(shard){var sel=document.querySelector('#pw_promos_shards');if(!sel)return '[]';sel.value=shard;var e=document.createEvent('HTMLEvents');e.initEvent('change',true,false);sel.dispatchEvent(e);var s2=document.querySelector('#pw_promos_chars');if(!s2)return '[]';return JSON.stringify(Array.from(s2.options).map(o=>({value:o.value,text:o.text})));})('" + EscapeJs(shard.value) + "')";
                    var charsJson = await browser.ExecuteScriptAsync(jsChars);
                    var chars = string.IsNullOrWhiteSpace(charsJson) ? new List<JsOption>() : JsonSerializer.Deserialize<List<JsOption>>(charsJson) ?? new List<JsOption>();

                    var server = new AccountServer
                    {
                        OptionId = shard.value ?? string.Empty,
                        Name = shard.text ?? string.Empty,
                        Characters = new List<AccountCharacter>(),
                        AccountId = account.Id
                    };
                    server.Characters = chars.Select(ch => new AccountCharacter
                    {
                        OptionId = ch.value ?? string.Empty,
                        Name = ch.text ?? string.Empty,
                        ServerId = server.Id
                    }).ToList();
                    servers.Add(server);
                    log.AppendLog($"   {server.Name}: персонажей — {server.Characters.Count}");
                }

                // Сохраняем в БД: добавляем новые сервера/персонажей, не трогая существующие
                await using (var db = new AppDbContext())
                {
                    // Находим VM-экземпляр аккаунта в текущих данных, если доступен
                    var acc = db.Accounts
                        .Include(a => a.Servers)
                            .ThenInclude(s => s.Characters)
                        .FirstOrDefault(a => a.Id == account.Id);
                    if (acc != null)
                    {
                        foreach (var server in servers)
                        {
                            var charsToAdd = new List<AccountCharacter>();
                            var existing = acc.Servers.FirstOrDefault(s => s.OptionId == server.OptionId);
                            if (existing != null)
                            {
                                var newChars = server.Characters
                                    .Where(ch => existing.Characters.All(c => c.OptionId != ch.OptionId))
                                    .ToList();
                                foreach (var newChar in newChars)
                                    newChar.ServerId = existing.Id;
                                charsToAdd.AddRange(newChars);
                            }
                            if (existing == null)
                            {
                                await db.AddAsync(server);
                                existing = server;
                                charsToAdd.AddRange(server.Characters);
                            }

                            if (existing.Characters.Count == 1)
                                existing.DefaultCharacterOptionId = existing.Characters.First().OptionId;

                            if (charsToAdd.Count > 0)
                                await db.AddRangeAsync(charsToAdd);
                        }

                        db.Update(acc);
                        await db.SaveChangesAsync();
                    }
                }

                log.MarkCompleted("Готово: данные о серверах и персонажах сохранены.");
            }
            catch (Exception ex)
            {
                log.AppendLog("Ошибка: " + ex.Message);
                log.MarkCompleted("Завершено с ошибкой");
            }
        }

        // Запускаем процесс и показываем модальное окно логов
        _ = RunAsync();
        log.ShowDialog();
    }

    private static string EscapeJs(string s)
    {
        return (s ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\"", "\\\"")
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty);
    }

    private class JsOption
    {
        public string value { get; set; } = string.Empty;
        public string text { get; set; } = string.Empty;
    }
}
