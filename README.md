# DotnetBinaryObjectSerializer

Serializador binário de objetos para .NET 8. Converte objetos em `byte[]` e permite reconstruí-los diretamente, lê-los como uma árvore de nós ou desserializá-los a partir de arquivos e streams.

## Recursos

- Tipos primitivos, strings, enums, valores nulos e `byte[]`
- Objetos e estruturas aninhadas
- Arrays, listas, conjuntos e outras coleções com `Add`
- Dicionários com chaves convertidas para texto
- Leitura direta como objeto, coleção ou árvore navegável
- Entrada por `byte[]`, `FileInfo` ou `Stream`
- Renomeação e exclusão de campos por atributos

## Requisitos

- .NET 8 SDK ou superior

## Instalação

Enquanto o projeto não estiver publicado no NuGet, adicione-o como referência de projeto:

```bash
dotnet add reference ../DotnetBinaryObjectSerializer/DotnetBinaryObjectSerializer.csproj
```

Ou inclua a referência diretamente no `.csproj`:

```xml
<ItemGroup>
  <ProjectReference Include="..\DotnetBinaryObjectSerializer\DotnetBinaryObjectSerializer.csproj" />
</ItemGroup>
```

## Uso básico

Os objetos desserializados precisam ter um construtor sem parâmetros, público ou privado. Por padrão, os campos de instância públicos e privados são serializados.

```csharp
using DotnetBinaryObjectSerializer.Mapper;

public class Product
{
    public string Name;
    public decimal Price;

    public Product() { }

    public Product(string name, decimal price)
    {
        Name = name;
        Price = price;
    }
}

var serializer = new BinaryObjectMapper();
var product = new Product("Keyboard", 299.90m);

byte[] bytes = serializer.EncodeToByteArray(product);
Product restored = serializer.ReadAsObject<Product>(bytes);
```

`BinaryObjectMapper` implementa codificação e decodificação. Também é possível usar `BinaryObjectEncoderMapper` e `BinaryObjectDecoderMapper` separadamente.

## Coleções

```csharp
var serializer = new BinaryObjectMapper();
var source = new List<string> { "one", "two", "three" };

byte[] bytes = serializer.EncodeToByteArray(source);
List<string> restored = serializer.ReadAsCollection<List<string>, string>(bytes);
```

## Leitura como árvore

A árvore permite consultar o conteúdo sem reconstruir previamente o tipo original:

```csharp
var serializer = new BinaryObjectMapper();
byte[] bytes = serializer.EncodeToByteArray(new Product("Mouse", 149.99m));

IBinaryObjectNode root = serializer.ReadAsTree(bytes);
string name = root.GetChild("Name").AsString();
decimal price = root.GetChild("Price").AsObject<decimal>();
```

Um nó também pode ser convertido por `AsObject<T>()`, `AsCollection<C, T>()`, `AsMap()`, `AsByteMap()` ou `AsBinaryObjectNodeMap()`.

## Arquivos e streams

```csharp
var serializer = new BinaryObjectMapper();

Product fromFile = serializer.ReadAsObject<Product>(new FileInfo("product.bin"));

using Stream stream = File.OpenRead("product.bin");
Product fromStream = serializer.ReadAsObject<Product>(stream);
```

## Atributos

### Renomear um campo

`ElementRef` define o nome gravado no conteúdo binário.

```csharp
using DotnetBinaryObjectSerializer.Annotations;

public class User
{
    [ElementRef("user_name")]
    public string Name;

    public User() { }
}
```

### Ignorar um campo

`IgnoreElement` ignora um campo na codificação e na decodificação. Uma fase específica pode ser informada com `SerializationType`.

```csharp
using DotnetBinaryObjectSerializer.Annotations;
using DotnetBinaryObjectSerializer.Enums;

public class Account
{
    public string Username;

    [IgnoreElement]
    public string Password;

    [IgnoreElement(SerializationType.DECODE)]
    public string LocalValue;

    public Account() { }
}
```

## Tipos suportados

| Categoria | Tipos |
| --- | --- |
| Inteiros | `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong` |
| Ponto flutuante | `float`, `double`, `decimal` |
| Texto | `string`, `char`, enums, `BigInteger` |
| Estruturas | objetos, arrays, coleções e dicionários |
| Outros | `bool`, `byte[]` e valores nulos |

O formato preserva os valores, mas não armazena metadados completos de tipo. O tipo de destino é fornecido pelas chamadas genéricas durante a desserialização. Referências circulares não são suportadas.

## Desenvolvimento

```bash
dotnet restore
dotnet build DotnetBinaryObjectSerializer.sln
dotnet test DotnetBinaryObjectSerializer.sln
```

## Licença

Distribuído sob a [licença MIT](LICENSE).
