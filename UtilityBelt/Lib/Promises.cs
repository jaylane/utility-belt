
using System;

namespace UtilityBelt.Lib {
    /*
    public enum PromiseState {
        Pending,
        Resolved,
        Rejected
    }

    public interface IPromise {
        IPromise<ConvertedT> Then<ConvertedT>(Func<IPromise<ConvertedT>> onResolved);
        IPromise<ConvertedT> Then<ConvertedT>(Func<IPromise<ConvertedT>> onResolved, Func<IPromise<ConvertedT>> onRejected);
    }

    public interface IPromise<PromisedT> {
        PromiseState CurState { get; }

        IPromise Then(Func<PromisedT, IPromise> onResolved);
        IPromise Then(Func<PromisedT, IPromise> onResolved, Func<PromisedT, IPromise> onRejected);

        IPromise<ConvertedT> Then<ConvertedT>(Func<PromisedT, IPromise<ConvertedT>> onResolved, Func<PromisedT, IPromise<ConvertedT>> onRejected);

        void Resolve(PromisedT value);

        void Reject(PromisedT value);
    }

    public class Promise<PromisedT> : IPromise<PromisedT> {
        private PromisedT resolveValue;
        private PromisedT rejectValue;

        public PromiseState CurState { get; private set; } = PromiseState.Pending;

        public Promise(PromiseState startingState = PromiseState.Pending) {
            
        }

        public IPromise Then(Func<PromisedT, IPromise> onResolved) {
            return Then(onResolved, null);
        }

        public IPromise Then(Func<PromisedT, IPromise> onResolved, Func<PromisedT, IPromise> onRejected) {
            return Then(onResolved, onRejected);
        }

        public IPromise<ConvertedT> Then<ConvertedT>(Func<PromisedT, IPromise<ConvertedT>> onResolved, Func<PromisedT, IPromise<ConvertedT>> onRejected) {
            if (CurState == PromiseState.Resolved) {
                return onResolved(resolveValue);
            }

            return onRejected(rejectValue);
        }

        public void Resolve(PromisedT value) {
            resolveValue = value;
            CurState = PromiseState.Resolved;
        }

        public void Reject(PromisedT value) {
            if (CurState != PromiseState.Pending) {
                throw new Exception("Attempted to reject a promise that is not in pending state: " + CurState);
            }
            rejectValue = value;
            CurState = PromiseState.Rejected;
        }

        public static IPromise<PromisedT> Resolved(PromisedT promisedValue) {
            var promise = new Promise<PromisedT>(PromiseState.Resolved);
            promise.resolveValue = promisedValue;
            return promise;
        }
    }
    */
}