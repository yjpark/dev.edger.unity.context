using System;
using System.Collections.Generic;

using Edger.Unity;
using Edger.Unity.Weak;

namespace Edger.Unity.Context {
    public abstract class Aspect : BlockMono {
    }

    public abstract class AspectLog {
        private static int _NextIdentity = 0;
        private int _Identity = _NextIdentity++;
        public int Identity { get => _Identity; }

        public readonly DateTime Time = DateTime.UtcNow;
        public readonly Type AspectType;

        public AspectLog(Aspect aspect) {
            AspectType = aspect.GetType();
        }
    }

    public interface IEventWatcher<TEvt> : IBlock {
        void OnEvent(Aspect aspect, TEvt evt);
    }

    public sealed class BlockEventWatcher<TEvt> : WeakBlock, IEventWatcher<TEvt> {
        private readonly Action<Aspect, TEvt> _Block;

        public BlockEventWatcher(IBlockOwner owner, Action<Aspect, TEvt> block) : base(owner) {
            _Block = block;
        }

        public void OnEvent(Aspect aspect, TEvt evt) {
            _Block(aspect, evt);
        }
    }
}