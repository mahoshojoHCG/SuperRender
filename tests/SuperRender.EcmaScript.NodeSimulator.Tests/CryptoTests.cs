using Xunit;

namespace SuperRender.EcmaScript.NodeSimulator.Tests;

public class CryptoTests
{
    [Fact]
    public void RandomUUID_Format()
    {
        var (engine, _) = TestHost.Create();
        var s = engine.RunString("require('crypto').randomUUID()");
        Assert.Matches(@"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", s);
    }

    [Fact]
    public void RandomBytes_Length()
    {
        var (engine, _) = TestHost.Create();
        Assert.Equal("16", engine.RunString("require('crypto').randomBytes(16).length + ''"));
    }

    [Fact]
    public void Sha256_KnownVector()
    {
        var (engine, _) = TestHost.Create();
        var code = @"
            const c = require('crypto');
            c.createHash('sha256').update('abc').digest('hex')";
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", engine.RunString(code));
    }

    [Fact]
    public void Md5_KnownVector()
    {
        var (engine, _) = TestHost.Create();
        var code = @"require('crypto').createHash('md5').update('hello').digest('hex')";
        Assert.Equal("5d41402abc4b2a76b9719d911017c592", engine.RunString(code));
    }

    [Fact]
    public void Hmac_Sha256KnownVector()
    {
        var (engine, _) = TestHost.Create();
        // RFC 4231 test case 1: key=0x0b*20, data='Hi There'
        var code = @"
            const c = require('crypto');
            const key = Buffer.alloc(20, 0x0b);
            c.createHmac('sha256', key).update('Hi There').digest('hex')";
        Assert.Equal("b0344c61d8db38535ca8afceaf0bf12b881dc200c9833da726e9376c2e32cff7", engine.RunString(code));
    }

    [Fact]
    public void TimingSafeEqual_Match()
    {
        var (engine, _) = TestHost.Create();
        var code = @"
            const c = require('crypto');
            c.timingSafeEqual(Buffer.from('abc'), Buffer.from('abc'))";
        Assert.Equal("true", engine.RunString(code));
    }

    [Fact]
    public void TimingSafeEqual_LengthMismatchThrows()
    {
        var (engine, _) = TestHost.Create();
        var code = @"
            try { require('crypto').timingSafeEqual(Buffer.from('a'), Buffer.from('ab')); 'no' }
            catch (e) { 'threw' }";
        Assert.Equal("threw", engine.RunString(code));
    }

    [Fact]
    public void Pbkdf2Sync_KnownVector()
    {
        var (engine, _) = TestHost.Create();
        // RFC 6070 (sha1) test vector 2: password='password', salt='salt', iters=2, len=20
        var code = @"
            const c = require('crypto');
            c.pbkdf2Sync('password','salt',2,20,'sha1').toString('hex')";
        Assert.Equal("ea6c014dc72d6f8ccd1ed92ace1d41f0d8de8957", engine.RunString(code));
    }

    [Fact]
    public void HashUpdate_IncrementalMatchesOneShot()
    {
        var (engine, _) = TestHost.Create();
        var code = @"
            const c = require('crypto');
            const inc = c.createHash('sha256');
            inc.update('hel');
            inc.update('lo');
            const a = inc.digest('hex');
            const b = c.createHash('sha256').update('hello').digest('hex');
            a === b";
        Assert.Equal("true", engine.RunString(code));
    }
}
