namespace Jint
{
    public class DefaultRealmFactory : IRealmFactory
    {
        /// <summary>
        /// https://tc39.es/ecma262/#sec-createrealm
        /// </summary>
        /// <param name="engine"></param>
        public Realm CreateRealm(Engine engine)
        {
            var realmRec = new Realm();
            CreateIntrinsics(engine, realmRec);
            return realmRec;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-createintrinsics
        /// </summary>
        protected virtual void CreateIntrinsics(Engine engine, Realm realmRec)
        {
            var intrinsics = new Intrinsics(engine);
            realmRec.Intrinsics = intrinsics;
        }
    }
}