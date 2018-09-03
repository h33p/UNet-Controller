#if ENABLE_MIRROR
using Mirror;
#else
using UnityEngine.Networking;
#endif

namespace GreenByteSoftware.UNetController
{
    class InputMessage : MessageBase
    {
        public Inputs[] inputs;
        private Inputs prevInput;
        private Inputs cInp;

        public override void Deserialize(NetworkReader reader)
        {
            //TODO: public void OnSendInputs (NetworkMessage msg) on Controller.cs
        }

        public override void Serialize(NetworkWriter writer)
        {
            writer.WritePackedUInt32((uint)inputs.Length);

            for (int i = 0; i < inputs.Length; i++)
            {
                cInp = inputs[i];
#if (CMD_CHECKSUM) //TODO: this method stills on Controller.cs
				cInp.checksum = GetCommandChecksum(cInp);
#endif
                WriteInputs(ref writer, cInp, prevInput);
                prevInput = cInp;
            }
        }

        private void WriteInputs(ref NetworkWriter writer, Inputs inp, Inputs prevInp)
        {
            uint mask = GetInputsBitMask(prevInp, inp);
            writer.WritePackedUInt32(mask);
            WriteInputs(ref writer, inp, mask);
        }

        //Creates the bitmask by comparing 2 different inputs
        private uint GetInputsBitMask(Inputs inp1, Inputs inp2)
        {
            uint mask = 0;
            if (inp2.timestamp % GameManager.settings.maxDeltaTicks == 0)
                return 0xFFFFFFFF;

            if (inp1.inputs != inp2.inputs) mask |= 1 << 0;
            if (inp1.x != inp2.x) mask |= 1 << 1;
            if (inp1.y != inp2.y) mask |= 1 << 2;
            if (inp1.keys != inp2.keys) mask |= 1 << 3;
            if (inp1.timestamp + 1 != inp2.timestamp) mask |= 1 << 4;
            if (inp1.servertick + 1 != inp2.servertick) mask |= 1 << 5;
            return mask;
        }

        private void WriteInputs(ref NetworkWriter writer, Inputs inp, uint mask)
        {
            if ((mask & (1 << 0)) != 0)
                writer.Write(inp.inputs);
            if ((mask & (1 << 1)) != 0)
                writer.Write(inp.x);
            if ((mask & (1 << 2)) != 0)
                writer.Write(inp.y);
            if ((mask & (1 << 3)) != 0)
                writer.WritePackedUInt32((uint)inp.keys);
            if ((mask & (1 << 4)) != 0)
                writer.WritePackedUInt32(inp.timestamp);
            if ((mask & (1 << 5)) != 0)
                writer.WritePackedUInt32(inp.servertick);
#if (CMD_CHECKSUM)
			writer.Write(inp.checksum);
#endif
        }
    }
}
