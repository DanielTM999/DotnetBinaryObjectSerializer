using System;
using System.Collections.Generic;
using System.Linq;
using DotnetBinaryObjectSerializer;
using DotnetBinaryObjectSerializer.Annotations;
using DotnetBinaryObjectSerializer.Enums;
using DotnetBinaryObjectSerializer.Mapper;
using Xunit;

namespace DotnetBinaryObjectSerializer.Tests
{
    public class CustomerSerializationTest
    {
        public class Product
        {
            public string name;
            public double price;
            public int quantity;
            public ISet<string> tags;

            public Product() { }

            public Product(string name, double price, int quantity, ISet<string> tags)
            {
                this.name = name;
                this.price = price;
                this.quantity = quantity;
                this.tags = tags;
            }

            public override bool Equals(object o)
            {
                if (o is not Product p) return false;
                return price.Equals(p.price)
                       && quantity == p.quantity
                       && name == p.name
                       && SetEquals(tags, p.tags);
            }

            public override int GetHashCode() => System.HashCode.Combine(name, price, quantity, tags?.Count ?? 0);

            private static bool SetEquals(ISet<string> a, ISet<string> b)
            {
                if (a == null) return b == null;
                if (b == null) return false;
                return a.SetEquals(b);
            }
        }

        public class Address
        {
            public string city;
            public int zip;
            public int[] apartmentNumbers;

            public Address() { }

            public Address(string city, int zip, int[] apartmentNumbers)
            {
                this.city = city;
                this.zip = zip;
                this.apartmentNumbers = apartmentNumbers;
            }

            public override bool Equals(object o)
            {
                if (o is not Address a) return false;
                return zip == a.zip && city == a.city && ArrayEquals(apartmentNumbers, a.apartmentNumbers);
            }

            public override int GetHashCode() => System.HashCode.Combine(city, zip, apartmentNumbers?.Length ?? 0);

            private static bool ArrayEquals(int[] a, int[] b)
            {
                if (a == null) return b == null;
                if (b == null) return false;
                return a.SequenceEqual(b);
            }
        }

        public class Customer
        {
            public string username;
            public List<Product> cart;
            public Address shippingAddress;

            public Customer() { }

            public Customer(string username, List<Product> cart, Address shippingAddress)
            {
                this.username = username;
                this.cart = cart;
                this.shippingAddress = shippingAddress;
            }

            public override bool Equals(object o)
            {
                if (o is not Customer c) return false;
                return username == c.username
                       && ListEquals(cart, c.cart)
                       && Equals(shippingAddress, c.shippingAddress);
            }

            public override int GetHashCode() => System.HashCode.Combine(username, cart?.Count ?? 0, shippingAddress);

            private static bool ListEquals(List<Product> a, List<Product> b)
            {
                if (a == null) return b == null;
                if (b == null) return false;
                return a.SequenceEqual(b);
            }
        }

        public class ByteArrayWrapper
        {
            public byte[] data;

            public ByteArrayWrapper() { }
            public ByteArrayWrapper(byte[] data) => this.data = data;
        }

        public class ObjectWithArray
        {
            public string name;
            public int[] numbers;

            public ObjectWithArray() { }
            public ObjectWithArray(string name, int[] numbers)
            {
                this.name = name;
                this.numbers = numbers;
            }
        }

        public enum TesteEnum
        {
            ENUM_1,
            ENUM_2,
            ENUM_3
        }

        public class PluginsFeedViewModel
        {
            public string title;
            public string url;
            public PluginsFeedType feedType;

            public PluginsFeedViewModel() { }
            public PluginsFeedViewModel(string title, string url, PluginsFeedType feedType)
            {
                this.title = title;
                this.url = url;
                this.feedType = feedType;
            }

            public enum PluginsFeedType
            {
                FEED,
                LOCAL
            }
        }

        public class ElementRefSource
        {
            [ElementRef("external_name")]
            public string name;
            public int quantity;

            public ElementRefSource() { }
            public ElementRefSource(string name, int quantity)
            {
                this.name = name;
                this.quantity = quantity;
            }
        }

        public class ElementRefTarget
        {
            [ElementRef("external_name")]
            public string renamed;
            public int quantity;
        }

        public class EncoderIgnoreModel
        {
            public string visible;
            [IgnoreElement]
            public string secret;

            public EncoderIgnoreModel() { }
            public EncoderIgnoreModel(string visible, string secret)
            {
                this.visible = visible;
                this.secret = secret;
            }
        }

        public class DecoderIgnoreSource
        {
            public string visible;
            public string ignored;

            public DecoderIgnoreSource() { }
            public DecoderIgnoreSource(string visible, string ignored)
            {
                this.visible = visible;
                this.ignored = ignored;
            }
        }

        public class DecoderIgnoreTarget
        {
            public string visible;
            [IgnoreElement(SerializationType.DECODE)]
            public string ignored = "default";
        }

        private readonly BinaryObjectEncoderMapper encoder = new();
        private readonly BinaryObjectDecoderMapper decoder = new();

        private Customer SerializeAndDeserialize(Customer customer)
        {
            var encoded = encoder.EncodeToByteArray(customer);
            return decoder.ReadAsTree(encoded).AsObject<Customer>();
        }

        private static ISet<string> SetOf(params string[] values) => new HashSet<string>(values);

        [Fact]
        public void TestElementRefOnDecoder()
        {
            var serialized = encoder.EncodeToByteArray(new ElementRefSource("mapped", 7));

            var direct = decoder.ReadAsObject<ElementRefTarget>(serialized);
            var fromTree = decoder.ReadAsTree(serialized).AsObject<ElementRefTarget>();

            Assert.Equal("mapped", direct.renamed);
            Assert.Equal(7, direct.quantity);
            Assert.Equal("mapped", fromTree.renamed);
            Assert.Equal(7, fromTree.quantity);
        }

        [Fact]
        public void TestIgnoreElementOnEncoder()
        {
            var serialized = encoder.EncodeToByteArray(new EncoderIgnoreModel("public", "private"));

            var tree = decoder.ReadAsTree(serialized);

            Assert.NotNull(tree.GetChild("visible"));
            Assert.Null(tree.GetChild("secret"));
        }

        [Fact]
        public void TestIgnoreElementOnDecoder()
        {
            var serialized = encoder.EncodeToByteArray(new DecoderIgnoreSource("public", "private"));

            var direct = decoder.ReadAsObject<DecoderIgnoreTarget>(serialized);
            var fromTree = decoder.ReadAsTree(serialized).AsObject<DecoderIgnoreTarget>();

            Assert.Equal("public", direct.visible);
            Assert.Equal("default", direct.ignored);
            Assert.Equal("public", fromTree.visible);
            Assert.Equal("default", fromTree.ignored);
        }

        [Fact]
        public void TestNormalCustomer()
        {
            var addr = new Address("São Paulo", 12345, new[] { 101, 102 });
            var p1 = new Product("Notebook", 3500.75, 2, SetOf("eletrônicos", "computador"));
            var p2 = new Product("Smartphone", 1999.90, 1, SetOf("eletrônicos", "celular"));
            var customer = new Customer("tech_guy", new List<Product> { p1, p2 }, addr);

            var decoded = SerializeAndDeserialize(customer);
            Assert.Equal(customer, decoded);
        }

        [Fact]
        public void TestNormalCustomerDirectReadAsObject()
        {
            var addr = new Address("São Paulo", 12345, new[] { 101, 102 });
            var p1 = new Product("Notebook", 3500.75, 2, SetOf("eletrônicos", "computador"));
            var p2 = new Product("Smartphone", 1999.90, 1, SetOf("eletrônicos", "celular"));
            var customer = new Customer("tech_guy", new List<Product> { p1, p2 }, addr);

            var encoded = encoder.EncodeToByteArray(customer);
            var decoded = decoder.ReadAsObject<Customer>(encoded);

            Assert.Equal(customer, decoded);
        }

        [Fact]
        public void TestEmptyCart()
        {
            var addr = new Address("Curitiba", 80010, Array.Empty<int>());
            var customer = new Customer("empty_cart", new List<Product>(), addr);

            var decoded = SerializeAndDeserialize(customer);
            Assert.Equal(customer, decoded);
        }

        [Fact]
        public void TestNullAddress()
        {
            var p = new Product("Notebook", 3500.75, 2, SetOf("eletrônicos", "computador"));
            var customer = new Customer("no_address", new List<Product> { p }, null);

            var decoded = SerializeAndDeserialize(customer);
            Assert.Equal(customer, decoded);
        }

        [Fact]
        public void TestNullTags()
        {
            var addr = new Address("São Paulo", 12345, new[] { 101, 102 });
            var p = new Product("Mouse", 149.99, 3, null);
            var customer = new Customer("null_tags", new List<Product> { p }, addr);

            var decoded = SerializeAndDeserialize(customer);
            Assert.Equal(customer, decoded);
        }
       
        [Fact]
        public void TestEmptyTags()
        {
            var addr = new Address("São Paulo", 12345, new[] { 101, 102 });
            var p = new Product("Teclado", 299.90, 1, new HashSet<string>());
            var customer = new Customer("empty_tags", new List<Product> { p }, addr);

            var decoded = SerializeAndDeserialize(customer);
            Assert.Equal(customer, decoded);
        }

        [Fact]
        public void TestExtremeValues()
        {
            var addr = new Address("São Paulo", 12345, new[] { 101, 102 });
            var p1 = new Product("Server", double.MaxValue, int.MaxValue, SetOf("hardware"));
            var p2 = new Product("Cheap Item", double.Epsilon, int.MinValue, SetOf("misc"));
            var customer = new Customer("extremes", new List<Product> { p1, p2 }, addr);

            var decoded = SerializeAndDeserialize(customer);
            Assert.Equal(customer, decoded);
        }

        [Fact]
        public void TestLongStrings()
        {
            var addr = new Address("São Paulo", 12345, new[] { 101, 102 });
            var p1 = new Product("", 0.0, 0, new HashSet<string>());
            var p2 = new Product(new string('L', 1000), 9999.99, 10, SetOf("longname"));
            var customer = new Customer("string_edge", new List<Product> { p1, p2 }, addr);

            var decoded = SerializeAndDeserialize(customer);
            Assert.Equal(customer, decoded);
        }

        [Fact]
        public void TestLargeDouble()
        {
            var addr = new Address("São Paulo", 12345, new[] { 101, 102 });
            var p = new Product("CryptoCoin", 1234567890.1234567, 1000, SetOf("finance"));
            var customer = new Customer("big_numbers", new List<Product> { p }, addr);

            var decoded = SerializeAndDeserialize(customer);
            Assert.Equal(customer, decoded);
        }

        [Fact]
        public void TestRawByteArrayRoundtrip()
        {
            var originalBytes = new byte[] { 1, 2, 3, 4, 5, 10, 20, 30 };

            var wrapper = new ByteArrayWrapper(originalBytes);
            var serialized = encoder.EncodeToByteArray(wrapper);

            var deserialized = decoder.ReadAsTree(serialized).AsObject<ByteArrayWrapper>();

            Assert.Equal(originalBytes, deserialized.data);
        }

        [Fact]
        public void TestObjectWithInternalArray()
        {
            var numbers = new[] { 100, 200, 300, 400 };
            var original = new ObjectWithArray("testArray", numbers);

            var serialized = encoder.EncodeToByteArray(original);

            var deserialized = decoder.ReadAsTree(serialized).AsObject<ObjectWithArray>();

            Assert.Equal(original.name, deserialized.name);
            Assert.Equal(original.numbers, deserialized.numbers);

            var reSerialized = encoder.EncodeToByteArray(deserialized);
            Assert.Equal(serialized, reSerialized);
        }

        [Fact]
        public void TestByteArray()
        {
            var bytesArray = new byte[] { 10, 20, 30, 40, 50 };

            var serialized = encoder.EncodeToByteArray(bytesArray);

            var deserialized = decoder.ReadAsTree(serialized).AsObject<byte[]>();

            var reSerialized = encoder.EncodeToByteArray(deserialized);
            Assert.Equal(serialized, reSerialized);
        }

        [Fact]
        public void TestCollectionString()
        {
            var strings = new List<string> { "Teste1", "Teste2" };

            var serialized = encoder.EncodeToByteArray(strings);

            var deserialized = decoder.ReadAsTree(serialized).AsCollection<List<string>, string>();

            var reSerialized = encoder.EncodeToByteArray(deserialized);
            Assert.Equal(serialized, reSerialized);
        }

        [Fact]
        public void TestEnum()
        {
            var enums = new List<TesteEnum> { TesteEnum.ENUM_1, TesteEnum.ENUM_2, TesteEnum.ENUM_3 };

            var serialized = encoder.EncodeToByteArray(enums);

            var deserialized = decoder.ReadAsTree(serialized).AsCollection<List<TesteEnum>, TesteEnum>();

            var reSerialized = encoder.EncodeToByteArray(deserialized);
            Assert.Equal(serialized, reSerialized);
        }

        [Fact]
        public void TestMapStringString()
        {
            var map = new Dictionary<string, string>
            {
                ["key1"] = "value1",
                ["key2"] = "value2"
            };

            var serialized = encoder.EncodeToByteArray(map);

            var mapDecode = decoder.ReadAsTree(serialized).AsMap();

            Assert.Equal(map.Count, mapDecode.Count);
            foreach (var kv in map)
            {
                Assert.True(mapDecode.ContainsKey(kv.Key), "Chave ausente: " + kv.Key);
                Assert.Equal(kv.Value, mapDecode[kv.Key]);
            }
        }

        [Fact]
        public void TestMapOfMapString()
        {
            var map = new Dictionary<string, object>
            {
                ["key1"] = new Dictionary<string, object> { ["key1.2"] = "value1.2" },
                ["key2"] = new Dictionary<string, object> { ["key2.2"] = "value2.2" }
            };

            var serialized = encoder.EncodeToByteArray(map);

            var mapDecode = decoder.ReadAsTree(serialized).AsMap();

            Assert.Equal(map.Count, mapDecode.Count);
        }

        [Fact]
        public void TestMapOfListString()
        {
            var map = new Dictionary<string, List<string>>
            {
                ["key1"] = new List<string> { "value1.1", "value1.2", "value1.3" },
                ["key2"] = new List<string> { "value2.1", "value2.2", "value2.3" }
            };

            var serialized = encoder.EncodeToByteArray(map);

            var mapDecode = decoder.ReadAsTree(serialized).AsMap();

            Assert.Equal(map.Count, mapDecode.Count);
        }

        [Fact]
        public void PluginsFeedViewModelMap()
        {
            var source = new Dictionary<string, object>
            {
                ["feeds"] = new List<PluginsFeedViewModel>
                {
                    new("teste", "testeUrl", PluginsFeedViewModel.PluginsFeedType.FEED)
                }
            };

            var serialized = encoder.EncodeToByteArray(source);

            var mapDecode = decoder.ReadAsTree(serialized).AsMap();

            Assert.NotNull(mapDecode);
        }
    }
}
