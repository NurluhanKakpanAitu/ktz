namespace KTZH.Models;

/// <summary>
/// Локомотив КТЖ (ТЭ33А или KZ8A)
/// </summary>
public class Locomotive
{
    /// <summary>Уникальный идентификатор (напр. "TE33A-001")</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Отображаемое имя</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Тип локомотива</summary>
    public LocomotiveType Type { get; set; }

    /// <summary>Серийный номер</summary>
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>Город депо приписки</summary>
    public string DepotCity { get; set; } = string.Empty;

    /// <summary>Текущая широта</summary>
    public double Latitude { get; set; }

    /// <summary>Текущая долгота</summary>
    public double Longitude { get; set; }

    /// <summary>Текущий маршрут</summary>
    public string CurrentRoute { get; set; } = string.Empty;
}