using System.ComponentModel.DataAnnotations;

namespace Arona.Datas.Storage;

public class StringConfig
{
    [Key]
    public required string Id { get; set; }

    public required string Value { get; set; }
}
