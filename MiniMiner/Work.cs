﻿using System;
using System.Security.Cryptography;
using System.Text;

namespace MiniMiner
{
    public class Work : IDisposable
    {
		private Pool _pool;
		private readonly SHA256Managed Hasher = new SHA256Managed();
		private readonly long _ticks;
		private readonly long _nonceOffset;
		public byte[] Data;
		public byte[] Current;
		public uint FinalNonce{ get; set;}
        public int WorkerID { get; set; }
        private string _paddedData;
        private uint _batchSize;

        public Work(Pool pool)
        {
            Data = pool.ParseData();
            Current = (byte[])Data.Clone();
            _nonceOffset = Data.Length - 4;
            _ticks = DateTime.Now.Ticks;
			_pool = pool;
			Hasher = new SHA256Managed ();
        }

        public Work(Work work)
        {
            Data = work.Data;
            Current = (byte[])work.Current.Clone();
            _nonceOffset = work._nonceOffset;
            _ticks = work._ticks;
            _pool = work._pool;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private bool _isDisposed;
        protected virtual void Dispose(bool isDisposing)
        {
            if (!_isDisposed)
            {
                _pool = null;
                _isDisposed = true;
            }
        }

        internal bool LookForShare(ref uint nonce, uint batchSize, int x)
        {
            _batchSize = batchSize;
            for(;batchSize > 0; batchSize--)
            {
                BitConverter.GetBytes(nonce).CopyTo(Current, _nonceOffset);
                var doubleHash = Sha256(Sha256(Current));

                var zeroBytes = 0; /* count trailing bytes that are zero */
                for (var i = 31; i >= 28; i--, zeroBytes++)
                    if(doubleHash[i] > 0)
                        break;

                //standard share difficulty matched! (target:ffffffffffffffffffffffffffffffffffffffffffffffffffffffff00000000)
                if(zeroBytes == 4)
                    return true;

                if ((uint.MaxValue - x) < nonce)
                    nonce = uint.MaxValue % (uint)x;
                else
                    nonce += (uint)x;
            }
            return false;
        }

        private byte[] Sha256(byte[] input)
        {
            return Hasher.ComputeHash(input, 0, input.Length);
        }

        public byte[] Hash
        {
            get { return Sha256(Sha256(Current)); }
        }

        public long Age 
        {
            get { return DateTime.Now.Ticks - _ticks; }
        }

		public void CalculateShare(uint nonce)
		{
			var data = Utils.EndianFlip32BitChunks(Utils.ToString(Current));
			_paddedData = Utils.AddPadding(data);
		}

		public bool SendShare()
		{
			return _pool.SendShare (_paddedData, FinalNonce);
		}

        private static DateTime _lastPrint = DateTime.Now;
        public string GetCurrentStateString(uint nonce)
        {
            var sb = new StringBuilder();
            sb.Append("Worker " + WorkerID + " Data: " + Utils.ToString(Data) + "\r\n");
            sb.Append(
                string.Concat("Nonce: ",
                Utils.ToString(nonce), "/",
                Utils.ToString(uint.MaxValue), " ",
                (((double)nonce / uint.MaxValue) * 100).ToString("F2"), "% \r\n"));
            sb.Append("Hash: " + Utils.ToString(Hash) + "\r\n");
            var span = DateTime.Now - _lastPrint;
            sb.Append("Speed: " + (int)((_batchSize / 1000) / span.TotalSeconds) + "Kh/s \r\n");
            _lastPrint = DateTime.Now;
            return sb.ToString();
        }
    }
}