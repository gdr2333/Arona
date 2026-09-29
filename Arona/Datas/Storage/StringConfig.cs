using System.ComponentModel.DataAnnotations;

namespace Arona.Datas.Storage;

public class StringConfig
{
    [Key]
    public string Id { get; set; }

    public string Value { get; set; }
}
