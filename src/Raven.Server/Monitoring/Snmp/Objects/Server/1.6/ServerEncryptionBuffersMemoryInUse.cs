﻿using Lextm.SharpSnmpLib;
using Sparrow;
using Voron.Impl;

namespace Raven.Server.Monitoring.Snmp.Objects.Server
{
    public sealed class ServerEncryptionBuffersMemoryInUse : ScalarObjectBase<Gauge32>
    {
        public ServerEncryptionBuffersMemoryInUse() : base(SnmpOids.Server.EncryptionBuffersMemoryInUse)
        {
        }

        protected override Gauge32 GetData()
        {
            var encryptionBuffers = EncryptionBuffersPool.Instance.GetStats();

            return new Gauge32(new Size(encryptionBuffers.CurrentlyInUseSize, SizeUnit.Bytes).GetValue(SizeUnit.Megabytes));
        }
    }
}
