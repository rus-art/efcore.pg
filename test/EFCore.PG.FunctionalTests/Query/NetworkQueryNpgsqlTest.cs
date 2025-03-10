using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net;
using System.Net.NetworkInformation;
using Npgsql.EntityFrameworkCore.PostgreSQL.TestUtilities;

// ReSharper disable ConvertToConstant.Local

namespace Npgsql.EntityFrameworkCore.PostgreSQL.Query;

/// <summary>
/// Provides unit tests for network address operator and function translations.
/// </summary>
/// <remarks>
/// See: https://www.postgresql.org/docs/current/static/functions-net.html
/// </remarks>
public class NetworkQueryNpgsqlTest : IClassFixture<NetworkQueryNpgsqlTest.NetworkAddressQueryNpgsqlFixture>
{
    private NetworkAddressQueryNpgsqlFixture Fixture { get; }

    // ReSharper disable once UnusedParameter.Local
    public NetworkQueryNpgsqlTest(NetworkAddressQueryNpgsqlFixture fixture, ITestOutputHelper testOutputHelper)
    {
        Fixture = fixture;
        Fixture.TestSqlLoggerFactory.Clear();
        // Fixture.TestSqlLoggerFactory.SetTestOutputHelper(testOutputHelper);
    }

    #region BugTests

    [Fact]
    public void Demonstrate_ValueTypeParametersAreDuplicated()
    {
        using var context = CreateContext();
        (IPAddress Address, int Subnet) cidr = (IPAddress.Any, default);

        var _ = context.NetTestEntities
            .Where(x => EF.Functions.ContainsOrEqual(x.Cidr, cidr))
            .Select(x => x.Cidr.Equals(cidr))
            .ToArray();

        AssertSql(
            @"@__cidr_1='(0.0.0.0, 0)' (DbType = Object)

SELECT n.""Cidr"" = @__cidr_1
FROM ""NetTestEntities"" AS n
WHERE n.""Cidr"" >>= @__cidr_1");
    }

    #endregion

    #region ParseTests

    [Fact]
    public void IPAddress_inet_parse_column()
    {
        using var context = CreateContext();
        var count = context.NetTestEntities.Count(x => x.Inet.Equals(IPAddress.Parse(x.TextInet)));

        Assert.Equal(9, count);
        AssertSql(
            @"SELECT COUNT(*)::INT
FROM ""NetTestEntities"" AS n
WHERE n.""Inet"" = n.""TextInet""::inet OR ((n.""Inet"" IS NULL) AND (n.""TextInet"" IS NULL))");
    }

    [Fact]
    public void PhysicalAddress_macaddr_parse_column()
    {
        using var context = CreateContext();
        var count = context.NetTestEntities.Count(x => x.Macaddr.Equals(PhysicalAddress.Parse(x.TextMacaddr)));

        Assert.Equal(9, count);
        AssertSql(
            @"SELECT COUNT(*)::INT
FROM ""NetTestEntities"" AS n
WHERE n.""Macaddr"" = n.""TextMacaddr""::macaddr OR ((n.""Macaddr"" IS NULL) AND (n.""TextMacaddr"" IS NULL))");
    }

    [Fact]
    public void IPAddress_inet_parse_literal()
    {
        using var context = CreateContext();
        var count = context.NetTestEntities.Count(x => x.Inet.Equals(IPAddress.Parse("192.168.1.2")));

        Assert.Equal(1, count);
        AssertSql(
            @"SELECT COUNT(*)::INT
FROM ""NetTestEntities"" AS n
WHERE n.""Inet"" = INET '192.168.1.2'");
    }

    [Fact]
    public void PhysicalAddress_macaddr_parse_literal()
    {
        using var context = CreateContext();
        var count = context.NetTestEntities.Count(x => x.Macaddr.Equals(PhysicalAddress.Parse("12-34-56-00-00-02")));

        Assert.Equal(1, count);
        AssertSql(
            @"SELECT COUNT(*)::INT
FROM ""NetTestEntities"" AS n
WHERE n.""Macaddr"" = MACADDR '123456000002'");
    }

    [Fact]
    public void IPAddress_inet_parse_parameter()
    {
        using var context = CreateContext();
        var inet = "192.168.1.2";
        var count = context.NetTestEntities.Count(x => x.Inet.Equals(IPAddress.Parse(inet)));

        Assert.Equal(1, count);
    }

    [Fact]
    public void PhysicalAddress_macaddr_parse_parameter()
    {
        using var context = CreateContext();
        var macaddr = "12-34-56-00-00-01";
        var count = context.NetTestEntities.Count(x => x.Macaddr.Equals(PhysicalAddress.Parse(macaddr)));

        Assert.Equal(1, count);
    }

    #endregion

    #region RelationalOperatorTests

    [Fact]
    public void IPAddress_inet_LessThan_inet()
    {
        using var context = CreateContext();
        var count = context.NetTestEntities.Count(x => EF.Functions.LessThan(x.Inet, IPAddress.Parse("192.168.1.7")));

        Assert.Equal(6, count);
        AssertSql(
            @"SELECT COUNT(*)::INT
FROM ""NetTestEntities"" AS n
WHERE n.""Inet"" < INET '192.168.1.7'");
    }

    [Fact]
    public void ValueTuple_cidr_LessThan_cidr()
    {
        using var context = CreateContext();
        (IPAddress Address, int Subnet) cidr = (IPAddress.Any, default);
        var _ = context.NetTestEntities
            .Where(x => EF.Functions.LessThan(x.Cidr, cidr))
            .ToArray();

        AssertSql(
            @"@__cidr_1='(0.0.0.0, 0)' (DbType = Object)

SELECT n.""Id"", n.""Cidr"", n.""Inet"", n.""Macaddr"", n.""Macaddr8"", n.""TextInet"", n.""TextMacaddr""
FROM ""NetTestEntities"" AS n
WHERE n.""Cidr"" < @__cidr_1");
    }

    [Fact]
    public void PhysicalAddress_macaddr_LessThan_macaddr()
    {
        using var context = CreateContext();
        var count = context.NetTestEntities.Count(x => EF.Functions.LessThan(x.Macaddr, PhysicalAddress.Parse("12-34-56-00-00-07")));

        Assert.Equal(6, count);
        AssertSql(
            @"SELECT COUNT(*)::INT
FROM ""NetTestEntities"" AS n
WHERE n.""Macaddr"" < MACADDR '123456000007'");
    }

    [ConditionalFact]
    [MinimumPostgresVersion(10, 0)]
    public void PhysicalAddress_macaddr8_LessThan_macaddr8()
    {
        using var context = CreateContext();
        var count = context.NetTestEntities.Count(x => EF.Functions.LessThan(x.Macaddr8, PhysicalAddress.Parse("08-00-2B-01-02-03-04-07")));

        Assert.Equal(6, count);
        AssertSql(
            @"SELECT COUNT(*)::INT
FROM ""NetTestEntities"" AS n
WHERE n.""Macaddr8"" < MACADDR8 '08002B0102030407'");
    }

    [Fact]
    public void IPAddress_inet_LessThanOrEqual_inet()
    {
        using var context = CreateContext();
        var count = context.NetTestEntities.Count(x => EF.Functions.LessThanOrEqual(x.Inet, IPAddress.Parse("192.168.1.7")));

        Assert.Equal(7, count);
        AssertSql(
            @"SELECT COUNT(*)::INT
FROM ""NetTestEntities"" AS n
WHERE n.""Inet"" <= INET '192.168.1.7'");
    }

    [Fact]
    public void ValueTuple_cidr_LessThanOrEqual_cidr()
    {
        using var context = CreateContext();
        (IPAddress Address, int Subnet) cidr = (IPAddress.Any, default);
        var _ = context.NetTestEntities
            .Where(x => EF.Functions.LessThanOrEqual(x.Cidr, cidr))
            .ToArray();

        AssertSql(
            @"@__cidr_1='(0.0.0.0, 0)' (DbType = Object)

SELECT n.""Id"", n.""Cidr"", n.""Inet"", n.""Macaddr"", n.""Macaddr8"", n.""TextInet"", n.""TextMacaddr""
FROM ""NetTestEntities"" AS n
WHERE n.""Cidr"" <= @__cidr_1");
    }

    [Fact]
    public void PhysicalAddress_macaddr_LessThanOrEqual_macaddr()
    {
        using var context = CreateContext();
        var count = context.NetTestEntities.Count(x => EF.Functions.LessThanOrEqual(x.Macaddr, PhysicalAddress.Parse("12-34-56-00-00-07")));

        Assert.Equal(7, count);
        AssertSql(
            @"SELECT COUNT(*)::INT
FROM ""NetTestEntities"" AS n
WHERE n.""Macaddr"" <= MACADDR '123456000007'");
    }

    [ConditionalFact]
    [MinimumPostgresVersion(10, 0)]
    public void PhysicalAddress_macaddr8_LessThanOrEqual_macaddr8()
    {
        using var context = CreateContext();
        var count = context.NetTestEntities.Count(x => EF.Functions.LessThanOrEqual(x.Macaddr8, PhysicalAddress.Parse("08-00-2B-01-02-03-04-07")));

        Assert.Equal(7, count);
        AssertSql(
            @"SELECT COUNT(*)::INT
FROM ""NetTestEntities"" AS n
WHERE n.""Macaddr8"" <= MACADDR8 '08002B0102030407'");
    }

    [Fact]
    public void IPAddress_inet_GreaterThanOrEqual_inet()
    {
        using var context = CreateContext();
        var count = context.NetTestEntities.Count(x => EF.Functions.GreaterThanOrEqual(x.Inet, IPAddress.Parse("192.168.1.7")));

        Assert.Equal(3, count);
        AssertSql(
            @"SELECT COUNT(*)::INT
FROM ""NetTestEntities"" AS n
WHERE n.""Inet"" >= INET '192.168.1.7'");
    }

    [Fact]
    public void ValueTuple_cidr_GreaterThanOrEqual_cidr()
    {
        using var context = CreateContext();
        (IPAddress Address, int Subnet) cidr = (IPAddress.Any, default);
        var _ = context.NetTestEntities
            .Where(x => EF.Functions.GreaterThanOrEqual(x.Cidr, cidr))
            .ToArray();

        AssertSql(
            @"@__cidr_1='(0.0.0.0, 0)' (DbType = Object)

SELECT n.""Id"", n.""Cidr"", n.""Inet"", n.""Macaddr"", n.""Macaddr8"", n.""TextInet"", n.""TextMacaddr""
FROM ""NetTestEntities"" AS n
WHERE n.""Cidr"" >= @__cidr_1");
    }

    [Fact]
    public void PhysicalAddress_macaddr_GreaterThanOrEqual_macaddr()
    {
        using var context = CreateContext();
        var count = context.NetTestEntities.Count(x => EF.Functions.GreaterThanOrEqual(x.Macaddr, PhysicalAddress.Parse("12-34-56-00-00-07")));

        Assert.Equal(3, count);
        AssertSql(
            @"SELECT COUNT(*)::INT
FROM ""NetTestEntities"" AS n
WHERE n.""Macaddr"" >= MACADDR '123456000007'");
    }

    [ConditionalFact]
    [MinimumPostgresVersion(10, 0)]
    public void PhysicalAddress_macaddr8_GreaterThanOrEqual_macaddr8()
    {
        using var context = CreateContext();
        var count = context.NetTestEntities.Count(x => EF.Functions.GreaterThanOrEqual(x.Macaddr8, PhysicalAddress.Parse("08-00-2B-01-02-03-04-07")));

        Assert.Equal(3, count);
        AssertSql(
            @"SELECT COUNT(*)::INT
FROM ""NetTestEntities"" AS n
WHERE n.""Macaddr8"" >= MACADDR8 '08002B0102030407'");
    }

    [Fact]
    public void IPAddress_inet_GreaterThan_inet()
    {
        using var context = CreateContext();
        var count = context.NetTestEntities.Count(x => EF.Functions.GreaterThan(x.Inet, IPAddress.Parse("192.168.1.7")));

        Assert.Equal(2, count);
        AssertSql(
            @"SELECT COUNT(*)::INT
FROM ""NetTestEntities"" AS n
WHERE n.""Inet"" > INET '192.168.1.7'");
    }

    [Fact]
    public void ValueTuple_cidr_GreaterThan_cidr()
    {
        using var context = CreateContext();
        (IPAddress Address, int Subnet) cidr = (IPAddress.Any, default);
        var _ = context.NetTestEntities
            .Where(x => EF.Functions.GreaterThan(x.Cidr, cidr))
            .ToArray();

        AssertSql(
            @"@__cidr_1='(0.0.0.0, 0)' (DbType = Object)

SELECT n.""Id"", n.""Cidr"", n.""Inet"", n.""Macaddr"", n.""Macaddr8"", n.""TextInet"", n.""TextMacaddr""
FROM ""NetTestEntities"" AS n
WHERE n.""Cidr"" > @__cidr_1");
    }

    [Fact]
    public void PhysicalAddress_macaddr_GreaterThan_macaddr()
    {
        using var context = CreateContext();
        var count = context.NetTestEntities.Count(x => EF.Functions.GreaterThan(x.Macaddr, PhysicalAddress.Parse("12-34-56-00-00-07")));

        Assert.Equal(2, count);
        AssertSql(
            @"SELECT COUNT(*)::INT
FROM ""NetTestEntities"" AS n
WHERE n.""Macaddr"" > MACADDR '123456000007'");
    }

    [ConditionalFact]
    [MinimumPostgresVersion(10, 0)]
    public void PhysicalAddress_macaddr8_GreaterThan_macaddr8()
    {
        using var context = CreateContext();
        var count = context.NetTestEntities.Count(x => EF.Functions.GreaterThan(x.Macaddr8, PhysicalAddress.Parse("08-00-2B-01-02-03-04-07")));

        Assert.Equal(2, count);
        AssertSql(
            @"SELECT COUNT(*)::INT
FROM ""NetTestEntities"" AS n
WHERE n.""Macaddr8"" > MACADDR8 '08002B0102030407'");
    }

    #endregion

    #region ContainmentOperatorTests

    [Fact]
    public void IPAddress_inet_ContainedBy_inet()
    {
        using var context = CreateContext();
        var inet = IPAddress.Any;
        var _ = context.NetTestEntities
            .Where(x => EF.Functions.ContainedBy(x.Inet, inet))
            .ToArray();

        AssertSql(
            @"@__inet_1='0.0.0.0' (DbType = Object)

SELECT n.""Id"", n.""Cidr"", n.""Inet"", n.""Macaddr"", n.""Macaddr8"", n.""TextInet"", n.""TextMacaddr""
FROM ""NetTestEntities"" AS n
WHERE n.""Inet"" << @__inet_1");
    }

    [Fact]
    public void IPAddress_inet_ContainedBy_cidr()
    {
        using var context = CreateContext();
        (IPAddress Address, int Subnet) cidr = (IPAddress.Any, default);
        var _ = context.NetTestEntities
            .Where(x => EF.Functions.ContainedBy(x.Inet, cidr))
            .ToArray();

        AssertSql(
            @"@__cidr_1='(0.0.0.0, 0)' (DbType = Object)

SELECT n.""Id"", n.""Cidr"", n.""Inet"", n.""Macaddr"", n.""Macaddr8"", n.""TextInet"", n.""TextMacaddr""
FROM ""NetTestEntities"" AS n
WHERE n.""Inet"" << @__cidr_1");
    }

    [Fact]
    public void ValueTuple_cidr_ContainedBy_cidr()
    {
        using var context = CreateContext();
        (IPAddress Address, int Subnet) cidr = (IPAddress.Any, default);
        var _ = context.NetTestEntities
            .Where(x => EF.Functions.ContainedBy(x.Cidr, cidr))
            .ToArray();

        AssertSql(
            @"@__cidr_1='(0.0.0.0, 0)' (DbType = Object)

SELECT n.""Id"", n.""Cidr"", n.""Inet"", n.""Macaddr"", n.""Macaddr8"", n.""TextInet"", n.""TextMacaddr""
FROM ""NetTestEntities"" AS n
WHERE n.""Cidr"" << @__cidr_1");
    }

    [Fact]
    public void IPAddress_inet_ContainedByOrEqual_inet()
    {
        using var context = CreateContext();
        var inet = IPAddress.Any;
        var _ = context.NetTestEntities
            .Where(x => EF.Functions.ContainedByOrEqual(x.Inet, inet))
            .ToArray();

        AssertSql(
            @"@__inet_1='0.0.0.0' (DbType = Object)

SELECT n.""Id"", n.""Cidr"", n.""Inet"", n.""Macaddr"", n.""Macaddr8"", n.""TextInet"", n.""TextMacaddr""
FROM ""NetTestEntities"" AS n
WHERE n.""Inet"" <<= @__inet_1");
    }

    [Fact]
    public void IPAddress_inet_ContainedByOrEqual_cidr()
    {
        using var context = CreateContext();
        (IPAddress Address, int Subnet) cidr = (IPAddress.Any, default);
        var _ = context.NetTestEntities
            .Where(x => EF.Functions.ContainedByOrEqual(x.Inet, cidr))
            .ToArray();

        AssertSql(
            @"@__cidr_1='(0.0.0.0, 0)' (DbType = Object)

SELECT n.""Id"", n.""Cidr"", n.""Inet"", n.""Macaddr"", n.""Macaddr8"", n.""TextInet"", n.""TextMacaddr""
FROM ""NetTestEntities"" AS n
WHERE n.""Inet"" <<= @__cidr_1");
    }

    [Fact]
    public void ValueTuple_cidr_ContainedByOrEqual_cidr()
    {
        using var context = CreateContext();
        (IPAddress Address, int Subnet) cidr = (IPAddress.Any, default);
        var _ = context.NetTestEntities
            .Where(x => EF.Functions.ContainedByOrEqual(x.Cidr, cidr))
            .ToArray();

        AssertSql(
            @"@__cidr_1='(0.0.0.0, 0)' (DbType = Object)

SELECT n.""Id"", n.""Cidr"", n.""Inet"", n.""Macaddr"", n.""Macaddr8"", n.""TextInet"", n.""TextMacaddr""
FROM ""NetTestEntities"" AS n
WHERE n.""Cidr"" <<= @__cidr_1");
    }

    [Fact]
    public void IPAddress_inet_Contains_inet()
    {
        using var context = CreateContext();
        var inet = IPAddress.Any;
        var _ = context.NetTestEntities
            .Where(x => EF.Functions.Contains(x.Inet, inet))
            .ToArray();

        AssertSql(
            @"@__inet_1='0.0.0.0' (DbType = Object)

SELECT n.""Id"", n.""Cidr"", n.""Inet"", n.""Macaddr"", n.""Macaddr8"", n.""TextInet"", n.""TextMacaddr""
FROM ""NetTestEntities"" AS n
WHERE n.""Inet"" >> @__inet_1");
    }

    [Fact]
    public void ValueTuple_cidr_Contains_inet()
    {
        using var context = CreateContext();
        var inet = IPAddress.Any;
        var _ = context.NetTestEntities
            .Where(x => EF.Functions.Contains(x.Cidr, inet))
            .ToArray();

        AssertSql(
            @"@__inet_1='0.0.0.0' (DbType = Object)

SELECT n.""Id"", n.""Cidr"", n.""Inet"", n.""Macaddr"", n.""Macaddr8"", n.""TextInet"", n.""TextMacaddr""
FROM ""NetTestEntities"" AS n
WHERE n.""Cidr"" >> @__inet_1");
    }

    [Fact]
    public void ValueTuple_cidr_Contains_cidr()
    {
        using var context = CreateContext();
        (IPAddress Address, int Subnet) cidr = (IPAddress.Any, default);
        var _ = context.NetTestEntities
            .Where(x => EF.Functions.Contains(x.Cidr, cidr))
            .ToArray();

        AssertSql(
            @"@__cidr_1='(0.0.0.0, 0)' (DbType = Object)

SELECT n.""Id"", n.""Cidr"", n.""Inet"", n.""Macaddr"", n.""Macaddr8"", n.""TextInet"", n.""TextMacaddr""
FROM ""NetTestEntities"" AS n
WHERE n.""Cidr"" >> @__cidr_1");
    }

    [Fact]
    public void IPAddress_inet_ContainsOrEqual_inet()
    {
        using var context = CreateContext();
        var inet = IPAddress.Any;
        var _ = context.NetTestEntities
            .Where(x => EF.Functions.ContainsOrEqual(x.Inet, inet))
            .ToArray();

        AssertSql(
            @"@__inet_1='0.0.0.0' (DbType = Object)

SELECT n.""Id"", n.""Cidr"", n.""Inet"", n.""Macaddr"", n.""Macaddr8"", n.""TextInet"", n.""TextMacaddr""
FROM ""NetTestEntities"" AS n
WHERE n.""Inet"" >>= @__inet_1");
    }

    [Fact]
    public void ValueTuple_cidr_ContainsOrEqual_inet()
    {
        using var context = CreateContext();
        var inet = IPAddress.Any;
        var _ = context.NetTestEntities
            .Where(x => EF.Functions.ContainsOrEqual(x.Cidr, inet))
            .ToArray();

        AssertSql(
            @"@__inet_1='0.0.0.0' (DbType = Object)

SELECT n.""Id"", n.""Cidr"", n.""Inet"", n.""Macaddr"", n.""Macaddr8"", n.""TextInet"", n.""TextMacaddr""
FROM ""NetTestEntities"" AS n
WHERE n.""Cidr"" >>= @__inet_1");
    }

    [Fact]
    public void ValueTuple_cidr_ContainsOrEqual_cidr()
    {
        using var context = CreateContext();
        (IPAddress Address, int Subnet) cidr = (IPAddress.Any, default);
        var _ = context.NetTestEntities
            .Where(x => EF.Functions.ContainsOrEqual(x.Cidr, cidr))
            .ToArray();

        AssertSql(
            @"@__cidr_1='(0.0.0.0, 0)' (DbType = Object)

SELECT n.""Id"", n.""Cidr"", n.""Inet"", n.""Macaddr"", n.""Macaddr8"", n.""TextInet"", n.""TextMacaddr""
FROM ""NetTestEntities"" AS n
WHERE n.""Cidr"" >>= @__cidr_1");
    }

    [Fact]
    public void IPAddress_inet_ContainsOrContainedBy_inet()
    {
        using var context = CreateContext();
        var inet = IPAddress.Any;
        var _ = context.NetTestEntities
            .Where(x => EF.Functions.ContainsOrContainedBy(x.Inet, inet))
            .ToArray();

        AssertSql(
            @"@__inet_1='0.0.0.0' (DbType = Object)

SELECT n.""Id"", n.""Cidr"", n.""Inet"", n.""Macaddr"", n.""Macaddr8"", n.""TextInet"", n.""TextMacaddr""
FROM ""NetTestEntities"" AS n
WHERE n.""Inet"" && @__inet_1");
    }

    [Fact]
    public void IPAddress_inet_ContainsOrContainedBy_cidr()
    {
        using var context = CreateContext();
        (IPAddress Address, int Subnet) cidr = (IPAddress.Any, default);
        var _ = context.NetTestEntities
            .Where(x => EF.Functions.ContainsOrContainedBy(x.Inet, cidr))
            .ToArray();

        AssertSql(
            @"@__cidr_1='(0.0.0.0, 0)' (DbType = Object)

SELECT n.""Id"", n.""Cidr"", n.""Inet"", n.""Macaddr"", n.""Macaddr8"", n.""TextInet"", n.""TextMacaddr""
FROM ""NetTestEntities"" AS n
WHERE n.""Inet"" && @__cidr_1");
    }

    [Fact]
    public void ValueTuple_cidr_ContainsOrContainedBy_inet()
    {
        using var context = CreateContext();
        var inet = IPAddress.Any;
        var _ = context.NetTestEntities
            .Where(x => EF.Functions.ContainsOrContainedBy(x.Cidr, inet))
            .ToArray();

        AssertSql(
            @"@__inet_1='0.0.0.0' (DbType = Object)

SELECT n.""Id"", n.""Cidr"", n.""Inet"", n.""Macaddr"", n.""Macaddr8"", n.""TextInet"", n.""TextMacaddr""
FROM ""NetTestEntities"" AS n
WHERE n.""Cidr"" && @__inet_1");
    }

    [Fact]
    public void ValueTuple_cidr_ContainsOrContainedBy_cidr()
    {
        using var context = CreateContext();
        (IPAddress Address, int Subnet) cidr = (IPAddress.Any, default);
        var _ = context.NetTestEntities
            .Where(x => EF.Functions.ContainsOrContainedBy(x.Cidr, cidr))
            .ToArray();

        AssertSql(
            @"@__cidr_1='(0.0.0.0, 0)' (DbType = Object)

SELECT n.""Id"", n.""Cidr"", n.""Inet"", n.""Macaddr"", n.""Macaddr8"", n.""TextInet"", n.""TextMacaddr""
FROM ""NetTestEntities"" AS n
WHERE n.""Cidr"" && @__cidr_1");
    }

    #endregion

    #region BitwiseOperatorTests

    [Fact]
    public void IPAddress_inet_BitwiseNot()
    {
        using var context = CreateContext();
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.BitwiseNot(x.Inet))
            .ToArray();

        AssertSql(
            @"SELECT ~n.""Inet""
FROM ""NetTestEntities"" AS n");
    }

    [Fact]
    public void ValueTuple_cidr_BitwiseNot()
    {
        using var context = CreateContext();
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.BitwiseNot(x.Cidr))
            .ToArray();

        AssertSql(
            @"SELECT ~n.""Cidr""
FROM ""NetTestEntities"" AS n");
    }

    [Fact]
    public void PhysicalAddress_macaddr_BitwiseNot()
    {
        using var context = CreateContext();
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.BitwiseNot(x.Macaddr))
            .ToArray();

        AssertSql(
            @"SELECT ~n.""Macaddr""
FROM ""NetTestEntities"" AS n");
    }

    [ConditionalFact]
    [MinimumPostgresVersion(10, 0)]
    public void PhysicalAddress_macaddr8_BitwiseNot()
    {
        using var context = CreateContext();
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.BitwiseNot(x.Macaddr8))
            .ToArray();

        AssertSql(
            @"SELECT ~n.""Macaddr8""
FROM ""NetTestEntities"" AS n");
    }

    [Fact]
    public void IPAddress_inet_BitwiseAnd_inet()
    {
        using var context = CreateContext();
        var inet = IPAddress.Any;
        var count = context.NetTestEntities.Count(x => x.Inet == EF.Functions.BitwiseAnd(x.Inet, inet));

        Assert.Equal(0, count);
        AssertSql(
            @"@__inet_1='0.0.0.0' (DbType = Object)

SELECT COUNT(*)::INT
FROM ""NetTestEntities"" AS n
WHERE n.""Inet"" = (n.""Inet"" & @__inet_1) OR (n.""Inet"" IS NULL)");
    }

    [Fact]
    public void ValueTuple_cidr_BitwiseAnd_cidr()
    {
        using var context = CreateContext();
        (IPAddress Address, int Subnet) cidr = (IPAddress.Any, default);
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.BitwiseAnd(x.Cidr, cidr))
            .ToArray();

        AssertSql(
            @"@__cidr_1='(0.0.0.0, 0)' (DbType = Object)

SELECT n.""Cidr"" & @__cidr_1
FROM ""NetTestEntities"" AS n");
    }

    [Fact]
    public void PhysicalAddress_macaddr_BitwiseAnd_macaddr()
    {
        using var context = CreateContext();
        var macaddr = new PhysicalAddress(new byte[6]);
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.BitwiseAnd(x.Macaddr, macaddr))
            .ToArray();

        AssertSql(
            @"@__macaddr_1='000000000000' (DbType = Object)

SELECT n.""Macaddr"" & @__macaddr_1
FROM ""NetTestEntities"" AS n");
    }

    [ConditionalFact]
    [MinimumPostgresVersion(10, 0)]
    public void PhysicalAddress_macaddr8_BitwiseAnd_macaddr8()
    {
        using var context = CreateContext();
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.BitwiseAnd(x.Macaddr8, x.Macaddr8))
            .ToArray();

        AssertSql(
            @"SELECT n.""Macaddr8"" & n.""Macaddr8""
FROM ""NetTestEntities"" AS n");
    }

    [Fact]
    public void IPAddress_inet_BitwiseOr_inet()
    {
        using var context = CreateContext();
        var inet = IPAddress.Any;
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.BitwiseOr(x.Inet, inet))
            .ToArray();

        AssertSql(
            @"@__inet_1='0.0.0.0' (DbType = Object)

SELECT n.""Inet"" | @__inet_1
FROM ""NetTestEntities"" AS n");
    }

    [Fact]
    public void ValueTuple_cidr_BitwiseOr_cidr()
    {
        using var context = CreateContext();
        (IPAddress Address, int Subnet) cidr = (IPAddress.Any, default);
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.BitwiseOr(x.Cidr, cidr))
            .ToArray();

        AssertSql(
            @"@__cidr_1='(0.0.0.0, 0)' (DbType = Object)

SELECT n.""Cidr"" | @__cidr_1
FROM ""NetTestEntities"" AS n");
    }

    [Fact]
    public void PhysicalAddress_macaddr_BitwiseOr_macaddr()
    {
        using var context = CreateContext();
        var macaddr = new PhysicalAddress(new byte[6]);
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.BitwiseOr(x.Macaddr, macaddr))
            .ToArray();

        AssertSql(
            @"@__macaddr_1='000000000000' (DbType = Object)

SELECT n.""Macaddr"" | @__macaddr_1
FROM ""NetTestEntities"" AS n");
    }

    [ConditionalFact]
    [MinimumPostgresVersion(10, 0)]
    public void PhysicalAddress_macaddr8_BitwiseOr_macaddr8()
    {
        using var context = CreateContext();
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.BitwiseOr(x.Macaddr8, x.Macaddr8))
            .ToArray();

        AssertSql(
            @"SELECT n.""Macaddr8"" | n.""Macaddr8""
FROM ""NetTestEntities"" AS n");
    }

    #endregion

    #region ArithmeticOperatorTests

    [Fact]
    public void IPAddress_inet_Add_int()
    {
        using var context = CreateContext();
        var actual = context.NetTestEntities.Single(x => EF.Functions.Add(x.Inet, 1) == IPAddress.Parse("192.168.1.2")).Inet;

        Assert.Equal(actual, IPAddress.Parse("192.168.1.1"));
        AssertSql(
            @"SELECT n.""Id"", n.""Cidr"", n.""Inet"", n.""Macaddr"", n.""Macaddr8"", n.""TextInet"", n.""TextMacaddr""
FROM ""NetTestEntities"" AS n
WHERE (n.""Inet"" + 1) = INET '192.168.1.2'
LIMIT 2");
    }

    [Fact]
    public void ValueTuple_cidr_Add_int()
    {
        using var context = CreateContext();
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.Add(x.Cidr, 1))
            .ToArray();

        AssertSql(
            @"SELECT n.""Cidr"" + 1
FROM ""NetTestEntities"" AS n");
    }

    [Fact]
    public void IPAddress_inet_Subtract_int()
    {
        using var context = CreateContext();
        var actual = context.NetTestEntities.Single(x => EF.Functions.Subtract(x.Inet, 1) == IPAddress.Parse("192.168.1.1")).Inet;

        Assert.Equal(actual, IPAddress.Parse("192.168.1.2"));
        AssertSql(
            @"SELECT n.""Id"", n.""Cidr"", n.""Inet"", n.""Macaddr"", n.""Macaddr8"", n.""TextInet"", n.""TextMacaddr""
FROM ""NetTestEntities"" AS n
WHERE (n.""Inet"" - 1) = INET '192.168.1.1'
LIMIT 2");
    }

    [Fact]
    public void ValueTuple_cidr_Subtract_int()
    {
        using var context = CreateContext();
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.Subtract(x.Cidr, 1))
            .ToArray();

        AssertSql(
            @"SELECT n.""Cidr"" - 1
FROM ""NetTestEntities"" AS n");
    }

    [Fact]
    public void IPAddress_inet_Subtract_inet()
    {
        using var context = CreateContext();
        var inet = IPAddress.Any;
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.Subtract(x.Inet, inet))
            .ToArray();

        AssertSql(
            @"@__inet_1='0.0.0.0' (DbType = Object)

SELECT n.""Inet"" - @__inet_1
FROM ""NetTestEntities"" AS n");
    }

    [Fact]
    public void ValueTuple_cidr_Subtract_cidr()
    {
        using var context = CreateContext();
        (IPAddress Address, int Subnet) cidr = (IPAddress.Any, default);
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.Subtract(x.Cidr, cidr))
            .ToArray();

        AssertSql(
            @"@__cidr_1='(0.0.0.0, 0)' (DbType = Object)

SELECT n.""Cidr"" - @__cidr_1
FROM ""NetTestEntities"" AS n");
    }

    #endregion

    #region FunctionTests

    [Fact]
    public void IPAddress_inet_Abbreviate()
    {
        using var context = CreateContext();
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.Abbreviate(x.Inet))
            .ToArray();

        AssertSql(
            @"SELECT abbrev(n.""Inet"")
FROM ""NetTestEntities"" AS n");
    }

    [Fact]
    public void ValueTuple_cidr_Abbreviate()
    {
        using var context = CreateContext();
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.Abbreviate(x.Cidr))
            .ToArray();

        AssertSql(
            @"SELECT abbrev(n.""Cidr"")
FROM ""NetTestEntities"" AS n");
    }

    [Fact]
    public void IPAddress_inet_Broadcast()
    {
        using var context = CreateContext();
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.Broadcast(x.Inet))
            .ToArray();

        AssertSql(
            @"SELECT broadcast(n.""Inet"")
FROM ""NetTestEntities"" AS n");
    }

    [Fact]
    public void ValueTuple_cidr_Broadcast()
    {
        using var context = CreateContext();
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.Broadcast(x.Cidr))
            .ToArray();

        AssertSql(
            @"SELECT broadcast(n.""Cidr"")
FROM ""NetTestEntities"" AS n");
    }

    [Fact]
    public void IPAddress_inet_Family()
    {
        using var context = CreateContext();
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.Family(x.Inet))
            .ToArray();

        AssertSql(
            @"SELECT family(n.""Inet"")
FROM ""NetTestEntities"" AS n");
    }

    [Fact]
    public void ValueTuple_cidr_Family()
    {
        using var context = CreateContext();
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.Family(x.Cidr))
            .ToArray();

        AssertSql(
            @"SELECT family(n.""Cidr"")
FROM ""NetTestEntities"" AS n");
    }

    [Fact]
    public void IPAddress_inet_Host()
    {
        using var context = CreateContext();
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.Host(x.Inet))
            .ToArray();

        AssertSql(
            @"SELECT host(n.""Inet"")
FROM ""NetTestEntities"" AS n");
    }

    [Fact]
    public void ValueTuple_cidr_Host()
    {
        using var context = CreateContext();
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.Host(x.Cidr))
            .ToArray();

        AssertSql(
            @"SELECT host(n.""Cidr"")
FROM ""NetTestEntities"" AS n");
    }

    [Fact]
    public void IPAddress_inet_HostMask()
    {
        using var context = CreateContext();
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.HostMask(x.Inet))
            .ToArray();

        AssertSql(
            @"SELECT hostmask(n.""Inet"")
FROM ""NetTestEntities"" AS n");
    }

    [Fact]
    public void ValueTuple_cidr_HostMask()
    {
        using var context = CreateContext();
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.HostMask(x.Cidr))
            .ToArray();

        AssertSql(
            @"SELECT hostmask(n.""Cidr"")
FROM ""NetTestEntities"" AS n");
    }

    [Fact]
    public void IPAddress_inet_MaskLength()
    {
        using var context = CreateContext();
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.MaskLength(x.Inet))
            .ToArray();

        AssertSql(
            @"SELECT masklen(n.""Inet"")
FROM ""NetTestEntities"" AS n");
    }

    [Fact]
    public void ValueTuple_cidr_MaskLength()
    {
        using var context = CreateContext();
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.MaskLength(x.Cidr))
            .ToArray();

        AssertSql(
            @"SELECT masklen(n.""Cidr"")
FROM ""NetTestEntities"" AS n");
    }

    [Fact]
    public void IPAddress_inet_Netmask()
    {
        using var context = CreateContext();
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.Netmask(x.Inet))
            .ToArray();

        AssertSql(
            @"SELECT netmask(n.""Inet"")
FROM ""NetTestEntities"" AS n");
    }

    [Fact]
    public void ValueTuple_cidr_Netmask()
    {
        using var context = CreateContext();
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.Netmask(x.Cidr))
            .ToArray();

        AssertSql(
            @"SELECT netmask(n.""Cidr"")
FROM ""NetTestEntities"" AS n");
    }

    [Fact]
    public void IPAddress_inet_Network()
    {
        using var context = CreateContext();
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.Network(x.Inet))
            .ToArray();

        AssertSql(
            @"SELECT network(n.""Inet"")
FROM ""NetTestEntities"" AS n");
    }

    [Fact]
    public void ValueTuple_cidr_Network()
    {
        using var context = CreateContext();
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.Network(x.Cidr))
            .ToArray();

        AssertSql(
            @"SELECT network(n.""Cidr"")
FROM ""NetTestEntities"" AS n");
    }

    [Fact]
    public void IPAddress_inet_SetMaskLength()
    {
        using var context = CreateContext();
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.SetMaskLength(x.Inet, default))
            .ToArray();

        AssertSql(
            @"SELECT set_masklen(n.""Inet"", 0)
FROM ""NetTestEntities"" AS n");
    }

    [Fact]
    public void ValueTuple_cidr_SetMaskLength()
    {
        using var context = CreateContext();
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.SetMaskLength(x.Cidr, default))
            .ToArray();

        AssertSql(
            @"SELECT set_masklen(n.""Cidr"", 0)
FROM ""NetTestEntities"" AS n");
    }

    [Fact]
    public void IPAddress_inet_Text()
    {
        using var context = CreateContext();
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.Text(x.Inet))
            .ToArray();

        AssertSql(
            @"SELECT text(n.""Inet"")
FROM ""NetTestEntities"" AS n");
    }

    [Fact]
    public void ValueTuple_cidr_Text()
    {
        using var context = CreateContext();
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.Text(x.Cidr))
            .ToArray();

        AssertSql(
            @"SELECT text(n.""Cidr"")
FROM ""NetTestEntities"" AS n");
    }

    [Fact]
    public void IPAddress_inet_SameFamily()
    {
        using var context = CreateContext();
        var inet = IPAddress.Any;
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.SameFamily(x.Inet, inet))
            .ToArray();

        AssertSql(
            @"@__inet_1='0.0.0.0' (DbType = Object)

SELECT inet_same_family(n.""Inet"", @__inet_1)
FROM ""NetTestEntities"" AS n");
    }

    [Fact]
    public void ValueTuple_cidr_SameFamily()
    {
        using var context = CreateContext();
        (IPAddress Address, int Subnet) cidr = (IPAddress.Any, default);
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.SameFamily(x.Cidr, cidr))
            .ToArray();

        AssertSql(
            @"@__cidr_1='(0.0.0.0, 0)' (DbType = Object)

SELECT inet_same_family(n.""Cidr"", @__cidr_1)
FROM ""NetTestEntities"" AS n");
    }

    [Fact]
    public void IPAddress_inet_Merge()
    {
        using var context = CreateContext();
        var inet = IPAddress.Any;
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.Merge(x.Inet, inet))
            .ToArray();

        AssertSql(
            @"@__inet_1='0.0.0.0' (DbType = Object)

SELECT inet_merge(n.""Inet"", @__inet_1)
FROM ""NetTestEntities"" AS n");
    }

    [Fact]
    public void ValueTuple_cidr_Merge()
    {
        using var context = CreateContext();
        (IPAddress Address, int Subnet) cidr = (IPAddress.Any, default);
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.Merge(x.Cidr, cidr))
            .ToArray();

        AssertSql(
            @"@__cidr_1='(0.0.0.0, 0)' (DbType = Object)

SELECT inet_merge(n.""Cidr"", @__cidr_1)
FROM ""NetTestEntities"" AS n");
    }

    [Fact]
    public void PhysicalAddress_macaddr_Truncate()
    {
        using var context = CreateContext();
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.Truncate(x.Macaddr))
            .ToArray();

        AssertSql(
            @"SELECT trunc(n.""Macaddr"")
FROM ""NetTestEntities"" AS n");
    }

    [ConditionalFact]
    [MinimumPostgresVersion(10, 0)]
    public void PhysicalAddress_macaddr8_Truncate()
    {
        using var context = CreateContext();
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.Truncate(x.Macaddr8))
            .ToArray();

        AssertSql(
            @"SELECT trunc(n.""Macaddr8"")
FROM ""NetTestEntities"" AS n");
    }

    [ConditionalFact]
    [MinimumPostgresVersion(10, 0)]
    public void PhysicalAddress_macaddr8_Set7BitMac8()
    {
        using var context = CreateContext();
        var _ = context.NetTestEntities
            .Select(x => EF.Functions.Set7BitMac8(x.Macaddr8))
            .ToArray();

        AssertSql(
            @"SELECT macaddr8_set7bit(n.""Macaddr8"")
FROM ""NetTestEntities"" AS n");
    }

    #endregion

    #region Fixtures

    /// <summary>
    /// Represents a fixture suitable for testing network address operators.
    /// </summary>
    public class NetworkAddressQueryNpgsqlFixture : SharedStoreFixtureBase<NetContext>
    {
        protected override string StoreName => "NetworkQueryTest";
        protected override ITestStoreFactory TestStoreFactory => NpgsqlTestStoreFactory.Instance;
        public TestSqlLoggerFactory TestSqlLoggerFactory => (TestSqlLoggerFactory)ListLoggerFactory;
        protected override void Seed(NetContext context) => NetContext.Seed(context);
    }

    /// <summary>
    /// Represents an entity suitable for testing network address operators.
    /// </summary>
    public class NetTestEntity
    {
        // ReSharper disable once UnusedMember.Global
        /// <summary>
        /// The primary key.
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// The network address.
        /// </summary>
        public IPAddress Inet { get; set; }

        /// <summary>
        /// The network address.
        /// </summary>
        public (IPAddress Address, int Subnet) Cidr { get; set; }

        /// <summary>
        /// The MAC address.
        /// </summary>
        public PhysicalAddress Macaddr { get; set; }

        /// <summary>
        /// The MAC address.
        /// </summary>
        [Column(TypeName = "macaddr8")]
        public PhysicalAddress Macaddr8 { get; set; }

        /// <summary>
        /// The text form of <see cref="Inet"/>.
        /// </summary>
        public string TextInet { get; set; }

        /// <summary>
        /// The text form of <see cref="Macaddr"/>.
        /// </summary>
        public string TextMacaddr { get; set; }
    }

    /// <summary>
    /// Represents a database suitable for testing network address operators.
    /// </summary>
    public class NetContext : PoolableDbContext
    {
        /// <summary>
        /// Represents a set of entities with <see cref="IPAddress"/> properties.
        /// </summary>
        public DbSet<NetTestEntity> NetTestEntities { get; set; }

        /// <summary>
        /// Initializes a <see cref="NetContext"/>.
        /// </summary>
        /// <param name="options">
        /// The options to be used for configuration.
        /// </param>
        public NetContext(DbContextOptions options) : base(options) {}

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            if (TestEnvironment.PostgresVersion < new Version(10, 0))
            {
                modelBuilder.Entity<NetTestEntity>().Ignore(x => x.Macaddr8);
            }

            base.OnModelCreating(modelBuilder);
        }

        public static void Seed(NetContext context)
        {
            for (var i = 1; i <= 9; i++)
            {
                var ip = IPAddress.Parse("192.168.1." + i);
                var macaddr = PhysicalAddress.Parse("12-34-56-00-00-0" + i);
                var macaddr8 = PhysicalAddress.Parse("08-00-2B-01-02-03-04-0" + i);
                context.NetTestEntities.Add(
                    new NetTestEntity
                    {
                        Id = i,
                        Inet = ip,
                        Cidr = (Address: IPAddress.Parse("192.168.1.0"), Subnet: 24),
                        Macaddr = macaddr,
                        Macaddr8 = macaddr8,
                        TextInet = ip.ToString(),
                        TextMacaddr = macaddr.ToString()
                    });
            }
            context.SaveChanges();
        }
    }

    #endregion

    #region Helpers

    protected NetContext CreateContext() => Fixture.CreateContext();

    private void AssertSql(params string[] expected) => Fixture.TestSqlLoggerFactory.AssertBaseline(expected);

    #endregion
}
