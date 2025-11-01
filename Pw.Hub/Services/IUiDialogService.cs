using System.Collections.Generic;
using System.Windows;

namespace Pw.Hub.Services;

/// <summary>
/// Абстракция для отображения диалогов/сообщений из ViewModel без прямой зависимости от MessageBox.
/// Позволяет тестировать бизнес-логику и подменять UI во время юнит-тестов.
/// </summary>
public interface IUiDialogService
{
    /// <summary>
    /// Показывает информационное/ошибочное сообщение пользователю.
    /// </summary>
    /// <param name="message">Текст сообщения.</param>
    /// <param name="caption">Заголовок окна (необязательно).</param>
    void Alert(string message, string caption = "PW Hub");

    /// <summary>
    /// Показывает запрос подтверждения с кнопками Да/Нет.
    /// </summary>
    /// <param name="message">Текст запроса.</param>
    /// <param name="caption">Заголовок окна (необязательно).</param>
    /// <returns>true, если пользователь подтвердил действие, иначе false.</returns>
    bool Confirm(string message, string caption = "Подтверждение");

    /// <summary>
    /// Запрос простого текстового ввода. На будущее; текущее приложение не использует.
    /// </summary>
    /// <param name="prompt">Текст подсказки.</param>
    /// <param name="caption">Заголовок окна.</param>
    /// <param name="defaultValue">Значение по умолчанию.</param>
    /// <returns>Введённая строка или null при отмене.</returns>
    string? Prompt(string prompt, string caption = "Ввод", string? defaultValue = null);

    /// <summary>
    /// Показывает диалог ввода аргументов запуска для Lua скрипта.
    /// Возвращает словарь введённых значений или null при отмене.
    /// </summary>
    /// <param name="inputs">Определения входных параметров.</param>
    Dictionary<string, object>? AskRunArguments(IList<InputDefinitionDto> inputs);
}
