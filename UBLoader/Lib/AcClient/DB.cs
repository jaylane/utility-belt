using System;

namespace AcClient {

    public unsafe struct Interface {
        // Struct:
        public Interface.Vtbl* vfptr;
        public override string ToString() => $"vfptr:->(Interface.Vtbl*)0x{(int)vfptr:X8}";
        public unsafe struct Vtbl {
            public static delegate* unmanaged[Thiscall]<Interface*, _GUID*, void**, int> IUnknown_QueryInterface; // HRESULT (__stdcall *IUnknown_QueryInterface)(Interface *this, _GUID *, void **);
            public static delegate* unmanaged[Thiscall]<Interface*, UInt32> IUnknown_AddRef; // unsigned int (__stdcall *IUnknown_AddRef)(Interface *this);
            public static delegate* unmanaged[Thiscall]<Interface*, UInt32> IUnknown_Release; // unsigned int (__stdcall *IUnknown_Release)(Interface *this);
            public static delegate* unmanaged[Thiscall]<Interface*, TResult*, Turbine_GUID*, void**, TResult*> QueryInterface; // TResult *(__thiscall *QueryInterface)(Interface *this, TResult *result, Turbine_GUID *, void **);
            public static delegate* unmanaged[Thiscall]<Interface*, UInt32> AddRef; // unsigned int (__thiscall *AddRef)(Interface *this);
            public static delegate* unmanaged[Thiscall]<Interface*, UInt32> Release; // unsigned int (__thiscall *Release)(Interface *this);
        }

        // Functions:

        // Interface.IUnknown_AddRef:
        public UInt32 IUnknown_AddRef() => ((delegate* unmanaged[Thiscall]<ref Interface, UInt32>)0x00401C10)(ref this); // .text:00401AE0 ; unsigned int __stdcall Interface::IUnknown_AddRef(Interface *this) .text:00401AE0 ?IUnknown_AddRef@Interface@@MAGKXZ

        // Interface.IUnknown_QueryInterface:
        public int IUnknown_QueryInterface(_GUID* iid, void** ppvObject) => ((delegate* unmanaged[Thiscall]<ref Interface, _GUID*, void**, int>)0x00401BF0)(ref this, iid, ppvObject); // .text:00401AC0 ; HRESULT __stdcall Interface::IUnknown_QueryInterface(Interface *this, _GUID *iid, void **ppvObject) .text:00401AC0 ?IUnknown_QueryInterface@Interface@@MAGJABU_GUID@@PAPAX@Z

        // Interface.IUnknown_Release:
        public UInt32 IUnknown_Release() => ((delegate* unmanaged[Thiscall]<ref Interface, UInt32>)0x00401C20)(ref this); // .text:00401AF0 ; unsigned int __stdcall Interface::IUnknown_Release(Interface *this) .text:00401AF0 ?IUnknown_Release@Interface@@MAGKXZ
    }




    public unsafe struct SerializeUsingPackDBObj {
        // Struct:
        public DBObj DBObj;
        public PackObj PackObj;
        public override string ToString() => $"DBObj(DBObj):{DBObj}, PackObj(PackObj):{PackObj}";


        // Functions:

        // SerializeUsingPackDBObj.Serialize:
        public static delegate* unmanaged[Thiscall]<SerializeUsingPackDBObj*, Archive*> Serialize = (delegate* unmanaged[Thiscall]<SerializeUsingPackDBObj*, Archive*>)0x004F80D0; // .text:004F7490 ; void __thiscall SerializeUsingPackDBObj::Serialize(SerializeUsingPackDBObj *this, Archive *io_archive) .text:004F7490 ?Serialize@SerializeUsingPackDBObj@@UAEXAAVArchive@@@Z

        // SerializeUsingPackDBObj.__vecDelDtor:
        // public static delegate* unmanaged[Thiscall]<SerializeUsingPackDBObj*,UInt32, void*> __vecDelDtor = (delegate* unmanaged[Thiscall]<SerializeUsingPackDBObj*,UInt32, void*>)0xDEADBEEF; // .text:004F7540 ; void *__thiscall SerializeUsingPackDBObj::`vector deleting destructor'(SerializeUsingPackDBObj *this, UInt32) .text:004F7540 ??_ESerializeUsingPackDBObj@@WDA@AEPAXI@Z

        // SerializeUsingPackDBObj.__Dtor:
        // public static delegate* unmanaged[Thiscall]<SerializeUsingPackDBObj*> __Dtor = (delegate* unmanaged[Thiscall]<SerializeUsingPackDBObj*>)0xDEADBEEF; // .text:004F7550 ; void __thiscall SerializeUsingPackDBObj::~SerializeUsingPackDBObj(SerializeUsingPackDBObj *this) .text:004F7550 ??1SerializeUsingPackDBObj@@UAE@XZ

        // SerializeUsingPackDBObj.__scaDelDtor:
        // public static delegate* unmanaged[Thiscall]<SerializeUsingPackDBObj*,UInt32, void*> __scaDelDtor = (delegate* unmanaged[Thiscall]<SerializeUsingPackDBObj*,UInt32, void*>)0xDEADBEEF; // .text:004F7B60 ; void *__thiscall SerializeUsingPackDBObj::`scalar deleting destructor'(SerializeUsingPackDBObj *this, UInt32) .text:004F7B60 ??_GSerializeUsingPackDBObj@@UAEPAXI@Z
    }

    public unsafe struct StreamPackObj {
        public PackObj packObj;
    };

    public unsafe struct PackObj {
        // Struct:
        public PackObj.Vtbl* vfptr;
        public override string ToString() => $"vfptr:->(PackObj.Vtbl*)0x{(int)vfptr:X8}";
        public unsafe struct Vtbl {
            public static delegate* unmanaged[Thiscall]<PackObj*, UInt32, void*> __vecDelDtor; // void *(__thiscall *__vecDelDtor)(PackObj *this, unsigned int);
        }

        // Functions:

        // PackObj.ALIGN_PTR:
        public static int ALIGN_PTR(void** ptr, UInt32* size) => ((delegate* unmanaged[Cdecl]<void**, UInt32*, int>)0x00500610)(ptr, size); // .text:004FFAF0 ; int __cdecl PackObj::ALIGN_PTR(void **ptr, unsigned int *size) .text:004FFAF0 ?ALIGN_PTR@PackObj@@SAHAAPAXAAI@Z

        // PackObj.ALIGN_PTR:
        public static UInt32 ALIGN_PTR(void** ptr) => ((delegate* unmanaged[Cdecl]<void**, UInt32>)0x004FD1B0)(ptr); // .text:004FC610 ; unsigned int __cdecl PackObj::ALIGN_PTR(void **ptr) .text:004FC610 ?ALIGN_PTR@PackObj@@SAIAAPAX@Z

        // PackObj.GET_SIZE_LEFT:
        public static UInt32 GET_SIZE_LEFT(void* addr, void* start, UInt32 size) => ((delegate* unmanaged[Cdecl]<void*, void*, UInt32, UInt32>)0x00526D90)(addr, start, size); // .text:00526190 ; unsigned int __cdecl PackObj::GET_SIZE_LEFT(void *addr, void *start, unsigned int size) .text:00526190 ?GET_SIZE_LEFT@PackObj@@SAIPAX0I@Z

        // PackObj.GetPackSize:
        public UInt32 GetPackSize() => ((delegate* unmanaged[Thiscall]<ref PackObj, UInt32>)0x00401090)(ref this); // .text:00401090 ; unsigned int __thiscall PackObj::GetPackSize(PackObj *this) .text:00401090 ?GetPackSize@PackObj@@UBEIXZ

        // PackObj.UNPACK_TYPE:
        public static int UNPACK_TYPE(int* data_r, void** buffer_vpr, UInt32* size_r) => ((delegate* unmanaged[Cdecl]<int*, void**, UInt32*, int>)0x004FD180)(data_r, buffer_vpr, size_r); // .text:004FC5E0 ; int __cdecl PackObj::UNPACK_TYPE(int *data_r, void **buffer_vpr, unsigned int *size_r) .text:004FC5E0 ?UNPACK_TYPE@PackObj@@SAHAAHAAPAXAAI@Z

        // PackObj.VERIFY_ADDR:
        public static int VERIFY_ADDR(void* addr, void* start, UInt32 size) => ((delegate* unmanaged[Cdecl]<void*, void*, UInt32, int>)0x00526DB0)(addr, start, size); // .text:005261B0 ; int __cdecl PackObj::VERIFY_ADDR(void *addr, void *start, unsigned int size) .text:005261B0 ?VERIFY_ADDR@PackObj@@SAHPAX0I@Z
    }



    public unsafe struct Turbine_RefCount {
        public _Vtbl* vfptr;
        public UInt32 m_cRef;
        public override string ToString() => $"m_cRef:{m_cRef:X8}";


        // Functions:

        // Turbine_RefCount.__scaDelDtor:
        public static delegate* unmanaged[Thiscall]<Turbine_RefCount*, UInt32, void*> __scaDelDtor = (delegate* unmanaged[Thiscall]<Turbine_RefCount*, UInt32, void*>)0x00401C30; // .text:00401B00 ; void *__thiscall Turbine_RefCount::`scalar deleting destructor'(Turbine_RefCount *this, UInt32) .text:00401B00 ??_GTurbine_RefCount@@UAEPAXI@Z

    };

    public unsafe struct DBObj {
        // Struct:
        public Interface a0;
        public UInt32 m_dataCategory;
        public Byte m_bLoaded;
        public Double m_timeStamp;
        public DBObj* m_pNext;
        public DBObj* m_pLast;
        public DBOCache* m_pMaintainer;
        public int m_numLinks;
        public UInt32 m_DID;
        public Byte m_AllowedInFreeList;
        public override string ToString() => $"a0(_Interface):{a0}, m_dataCategory:{m_dataCategory:X8}, m_bLoaded:{m_bLoaded:X2}, m_timeStamp:{m_timeStamp:n5}, m_pNext:->(DBObj*)0x{(int)m_pNext:X8}, m_pLast:->(DBObj*)0x{(int)m_pLast:X8}, m_pMaintainer:->(DBOCache*)0x{(int)m_pMaintainer:X8}, m_numLinks(int):{m_numLinks}, m_DID:{m_DID:X8}, m_AllowedInFreeList:{m_AllowedInFreeList:X2}";

        // Functions:

        // DBObj.__Ctor:
        public void __Ctor(UInt32 id) => ((delegate* unmanaged[Thiscall]<ref DBObj, UInt32, void>)0x00415460)(ref this, id); // .text:004151C0 ; void __thiscall DBObj::DBObj(DBObj *this, IDClass<_tagDataID,32,0> id) .text:004151C0 ??0DBObj@@IAE@V?$IDClass@U_tagDataID@@$0CA@$0A@@@@Z

        // DBObj.AddRef:
        public UInt32 AddRef() => ((delegate* unmanaged[Thiscall]<ref DBObj, UInt32>)0x004153D0)(ref this); // .text:00415130 ; unsigned int __thiscall DBObj::AddRef(DBObj *this) .text:00415130 ?AddRef@DBObj@@UBEKXZ

        // DBObj.AddToDataGraph:
        public void AddToDataGraph() => ((delegate* unmanaged[Thiscall]<ref DBObj, void>)0x004153C0)(ref this); // .text:00415120 ; void __thiscall DBObj::AddToDataGraph(DBObj *this) .text:00415120 ?AddToDataGraph@DBObj@@QBEXXZ

        // DBObj.FillDataGraph:
        public void FillDataGraph(IDataGraph* graph) => ((delegate* unmanaged[Thiscall]<ref DBObj, IDataGraph*, void>)0x00415760)(ref this, graph); // .text:004154C0 ; void __thiscall DBObj::FillDataGraph(DBObj *this, IDataGraph *graph) .text:004154C0 ?FillDataGraph@DBObj@@UBEXAAVIDataGraph@@@Z

        // DBObj.Get:
        public static DBObj* Get(QualifiedDataID* qdid) => ((delegate* unmanaged[Cdecl]<QualifiedDataID*, DBObj*>)0x00415430)(qdid); // .text:00415190 ; DBObj *__cdecl DBObj::Get(QualifiedDataID *qdid) .text:00415190 ?Get@DBObj@@KAPAV1@ABUQualifiedDataID@@@Z

        // DBObj.GetByEnum:
        public static DBObj* GetByEnum(int enum_id, int enum_group, UInt32 MyType) => ((delegate* unmanaged[Cdecl]<int, int, UInt32, DBObj*>)0x00415730)(enum_id, enum_group, MyType); // .text:00415490 ; DBObj *__cdecl DBObj::GetByEnum(int enum_id, int enum_group, unsigned int MyType) .text:00415490 ?GetByEnum@DBObj@@KAPAV1@JJK@Z

        // DBObj.GetDIDByEnum:
        public static UInt32* GetDIDByEnum(UInt32* result, int enum_id, int enum_group) => ((delegate* unmanaged[Cdecl]<UInt32*, int, int, UInt32*>)0x00415640)(result, enum_id, enum_group); // .text:004153A0 ; IDClass<_tagDataID,32,0> *__cdecl DBObj::GetDIDByEnum(IDClass<_tagDataID,32,0> *result, int enum_id, int enum_group) .text:004153A0 ?GetDIDByEnum@DBObj@@SA?AV?$IDClass@U_tagDataID@@$0CA@$0A@@@JJ@Z

        // DBObj.InitLoad:
        public static Byte InitLoad() => ((delegate* unmanaged[Cdecl]<Byte>)0x004154A0)(); // .text:00415200 ; bool __cdecl DBObj::InitLoad() .text:00415200 ?InitLoad@DBObj@@UAE_NXZ

        // DBObj.PreFetch:
        public static CACHE_OBJECT_CODES PreFetch(QualifiedDataID* qdid) => ((delegate* unmanaged[Cdecl]<QualifiedDataID*, CACHE_OBJECT_CODES>)0x00415450)(qdid); // .text:004151B0 ; CACHE_OBJECT_CODES __cdecl DBObj::PreFetch(QualifiedDataID *qdid) .text:004151B0 ?PreFetch@DBObj@@SA?AW4CACHE_OBJECT_CODES@@ABUQualifiedDataID@@@Z

        // DBObj.QueryInterface:
        public TResult* QueryInterface(TResult* result, Turbine_GUID* i_rcInterface, void** o_ppObject) => ((delegate* unmanaged[Thiscall]<ref DBObj, TResult*, Turbine_GUID*, void**, TResult*>)0x004154C0)(ref this, result, i_rcInterface, o_ppObject); // .text:00415220 ; TResult *__thiscall DBObj::QueryInterface(DBObj *this, TResult *result, Turbine_GUID *i_rcInterface, void **o_ppObject) .text:00415220 ?QueryInterface@DBObj@@UAE?AVTResult@@ABUTurbine_GUID@@PAPAX@Z

        // DBObj.Release:
        public UInt32 Release() => ((delegate* unmanaged[Thiscall]<ref DBObj, UInt32>)0x00415400)(ref this); // .text:00415160 ; unsigned int __thiscall DBObj::Release(DBObj *this) .text:00415160 ?Release@DBObj@@UBEKXZ

        // DBObj.ReloadFromDisk:
        public Byte ReloadFromDisk() => ((delegate* unmanaged[Thiscall]<ref DBObj, Byte>)0x00415520)(ref this); // .text:00415280 ; bool __thiscall DBObj::ReloadFromDisk(DBObj *this) .text:00415280 ?ReloadFromDisk@DBObj@@UBE_NXZ

        // DBObj.Remove:
        public static void Remove(DBObj* pObj) => ((delegate* unmanaged[Cdecl]<DBObj*, void>)0x00415610)(pObj); // .text:00415370 ; void __cdecl DBObj::Remove(DBObj *pObj) .text:00415370 ?Remove@DBObj@@SAXPBV1@@Z

        // DBObj.SaveToDisk:
        public Byte SaveToDisk(PreprocHeader* header) => ((delegate* unmanaged[Thiscall]<ref DBObj, PreprocHeader*, Byte>)0x00415550)(ref this, header); // .text:004152B0 ; bool __thiscall DBObj::SaveToDisk(DBObj *this, PreprocHeader *header) .text:004152B0 ?SaveToDisk@DBObj@@UBE_NABVPreprocHeader@@@Z

        // DBObj.Serialize:
        public void Serialize(Archive* io_archive) => ((delegate* unmanaged[Thiscall]<ref DBObj, Archive*, void>)0x00415590)(ref this, io_archive); // .text:004152F0 ; void __thiscall DBObj::Serialize(DBObj *this, Archive *io_archive) .text:004152F0 ?Serialize@DBObj@@UAEXAAVArchive@@@Z
    }

    public unsafe struct DBOCache {
        // Struct:
        public DBOCache.Vtbl* vfptr;
        public AutoGrowHashTable<UInt32, PTR<DBObj>> m_ObjTable;
        public UInt32 m_dbtype;
        public HashTable<UInt32, Single> m_fidelityTable;
        public Byte m_fCanKeepFreeObjs;
        public Byte m_fKeepFreeObjs;
        public Byte m_bFreelistActive;
        public FreelistDef m_freelistDef;
        public DBObj* m_pOldestFree;
        public DBObj* m_pYoungestFree;
        public UInt32 m_nFree;
        public UInt32 m_nTotalCount;
        public static delegate* unmanaged[Cdecl]<DBObj*> m_pfnAllocator; // DBObj *(__cdecl *m_pfnAllocator)();
        public override string ToString() => $"vfptr:->(DBOCache.Vtbl*)0x{(int)vfptr:X8}, m_ObjTable(AutoGrowHashTable<UInt32,DBObj*>):{m_ObjTable}, m_dbtype:{m_dbtype:X8}, m_fidelityTable(HashTable<UInt32,Single,0>):{m_fidelityTable}, m_fCanKeepFreeObjs:{m_fCanKeepFreeObjs:X2}, m_fKeepFreeObjs:{m_fKeepFreeObjs:X2}, m_bFreelistActive:{m_bFreelistActive:X2}, m_freelistDef(FreelistDef):{m_freelistDef}, m_pOldestFree:->(DBObj*)0x{(int)m_pOldestFree:X8}, m_pYoungestFree:->(DBObj*)0x{(int)m_pYoungestFree:X8}, m_nFree:{m_nFree:X8}, m_nTotalCount:{m_nTotalCount:X8}";
        public unsafe struct Vtbl {
            public static delegate* unmanaged[Thiscall]<DBOCache*, UInt32, void*> __vecDelDtor; // void *(__thiscall *__vecDelDtor)(DBOCache *this, unsigned int);
            public fixed byte gap4[8];
            public static delegate* unmanaged[Thiscall]<DBOCache*, UInt32, _Collection*> GetCollection; // struct Collection *(__thiscall *GetCollection)(DBOCache *this, IDClass<_tagDataID,32,0>);
            public static delegate* unmanaged[Thiscall]<DBOCache*, _Collection*, Byte> SetCollection; // bool (__thiscall *SetCollection)(DBOCache *this, struct Collection *);
            public static delegate* unmanaged[Thiscall]<DBOCache*, UInt32, UInt32> Release; // unsigned int (__thiscall *Release)(DBOCache *this, IDClass<_tagDataID,32,0>);
            public static delegate* unmanaged[Thiscall]<DBOCache*, DBObj*, Byte> AddObj; // bool (__thiscall *AddObj)(DBOCache *this, DBObj *);
            public static delegate* unmanaged[Thiscall]<DBOCache*, UInt32, Byte> RemoveObj; // bool (__thiscall *RemoveObj)(DBOCache *this, IDClass<_tagDataID,32,0>);
            public static delegate* unmanaged[Thiscall]<DBOCache*, Byte> CanLoadFromDisk; // bool (__thiscall *CanLoadFromDisk)(DBOCache *this);
            public static delegate* unmanaged[Thiscall]<DBOCache*, Byte> CanRequestFromNet; // bool (__thiscall *CanRequestFromNet)(DBOCache *this);
            public static delegate* unmanaged[Thiscall]<DBOCache*, void> FlushFreeObjects; // void (__thiscall *FlushFreeObjects)(DBOCache *this);
            public static delegate* unmanaged[Thiscall]<DBOCache*, PreprocHeader*, DBObj*, Byte> SaveObjectToDisk; // bool (__thiscall *SaveObjectToDisk)(DBOCache *this, PreprocHeader *, DBObj *);
            public static delegate* unmanaged[Thiscall]<DBOCache*, UInt32, Byte> ReloadObject; // bool (__thiscall *ReloadObject)(DBOCache *this, IDClass<_tagDataID,32,0>);
            public static delegate* unmanaged[Thiscall]<DBOCache*, void> LastWords; // void (__thiscall *LastWords)(DBOCache *this);
            public static delegate* unmanaged[Thiscall]<DBOCache*, DBObj*, Byte> AddObj_Internal; // bool (__thiscall *AddObj_Internal)(DBOCache *this, DBObj *);
            public static delegate* unmanaged[Thiscall]<DBOCache*, DBObj*, void> RemoveObj_Internal; // void (__thiscall *RemoveObj_Internal)(DBOCache *this, DBObj *);
            public static delegate* unmanaged[Thiscall]<DBOCache*, DBObj*, void> FreeObject; // void (__thiscall *FreeObject)(DBOCache *this, DBObj *);
            public static delegate* unmanaged[Thiscall]<DBOCache*, DBObj*, void> DestroyObj; // void (__thiscall *DestroyObj)(DBOCache *this, DBObj *);
            public static delegate* unmanaged[Thiscall]<DBOCache*, DBObj*, void> FreelistAdd; // void (__thiscall *FreelistAdd)(DBOCache *this, DBObj *);
            public static delegate* unmanaged[Thiscall]<DBOCache*, DBObj*, void> FreelistRemove; // void (__thiscall *FreelistRemove)(DBOCache *this, DBObj *);
            public static delegate* unmanaged[Thiscall]<DBOCache*, DBObj*> FreelistRemoveOldest; // DBObj *(__thiscall *FreelistRemoveOldest)(DBOCache *this);
        }

        // Functions:

        // DBOCache.__Ctor:
        // public void __Ctor(DBObj* a1, __cdecl* _allocator, UInt32 _dbtype) => ((delegate* unmanaged[Thiscall]<ref DBOCache, DBObj*, __cdecl*, UInt32, void>)0xDEADBEEF)(ref this, a1, _allocator, _dbtype); // .text:00417260 ; void __thiscall DBOCache::DBOCache(DBOCache *this, DBObj *(__cdecl *_allocator)(), unsigned int _dbtype) .text:00417260 ??0DBOCache@@QAE@P6APAVDBObj@@XZK@Z

        // DBOCache.AddObj:
        public Byte AddObj(DBObj* obj_p) => ((delegate* unmanaged[Thiscall]<ref DBOCache, DBObj*, Byte>)0x00416950)(ref this, obj_p); // .text:004166B0 ; bool __thiscall DBOCache::AddObj(DBOCache *this, DBObj *obj_p) .text:004166B0 ?AddObj@DBOCache@@UAE_NPAVDBObj@@@Z

        // DBOCache.AddObj_Internal:
        // public Byte AddObj_Internal(DBObj* object_p) => ((delegate* unmanaged[Thiscall]<ref DBOCache, DBObj*, Byte>)0xDEADBEEF)(ref this, object_p); // .text:00417130 ; bool __thiscall DBOCache::AddObj_Internal(DBOCache *this, DBObj *object_p) .text:00417130 ?AddObj_Internal@DBOCache@@MAE_NPAVDBObj@@@Z

        // DBOCache.DestroyObj:
        public void DestroyObj(DBObj* object_p) => ((delegate* unmanaged[Thiscall]<ref DBOCache, DBObj*, void>)0x00416C80)(ref this, object_p); // .text:00416A30 ; void __thiscall DBOCache::DestroyObj(DBOCache *this, DBObj *object_p) .text:00416A30 ?DestroyObj@DBOCache@@MAEXPAVDBObj@@@Z

        // DBOCache.FlushFreeObjects:
        public void FlushFreeObjects() => ((delegate* unmanaged[Thiscall]<ref DBOCache, void>)0x00416B10)(ref this); // .text:00416870 ; void __thiscall DBOCache::FlushFreeObjects(DBOCache *this) .text:00416870 ?FlushFreeObjects@DBOCache@@UAEXXZ

        // DBOCache.FreeObject:
        public void FreeObject(DBObj* object_p) => ((delegate* unmanaged[Thiscall]<ref DBOCache, DBObj*, void>)0x004169A0)(ref this, object_p); // .text:00416700 ; void __thiscall DBOCache::FreeObject(DBOCache *this, DBObj *object_p) .text:00416700 ?FreeObject@DBOCache@@MAEXPAVDBObj@@@Z

        // DBOCache.FreelistAdd:
        public void FreelistAdd(DBObj* object_p) => ((delegate* unmanaged[Thiscall]<ref DBOCache, DBObj*, void>)0x00416B50)(ref this, object_p); // .text:004168B0 ; void __thiscall DBOCache::FreelistAdd(DBOCache *this, DBObj *object_p) .text:004168B0 ?FreelistAdd@DBOCache@@MAEXPAVDBObj@@@Z

        // DBOCache.FreelistRemove:
        public void FreelistRemove(DBObj* object_p) => ((delegate* unmanaged[Thiscall]<ref DBOCache, DBObj*, void>)0x004169F0)(ref this, object_p); // .text:00416750 ; void __thiscall DBOCache::FreelistRemove(DBOCache *this, DBObj *object_p) .text:00416750 ?FreelistRemove@DBOCache@@MAEXPAVDBObj@@@Z

        // DBOCache.FreelistRemoveOldest:
        public DBObj* FreelistRemoveOldest() => ((delegate* unmanaged[Thiscall]<ref DBOCache, DBObj*>)0x00416A50)(ref this); // .text:004167B0 ; DBObj *__thiscall DBOCache::FreelistRemoveOldest(DBOCache *this) .text:004167B0 ?FreelistRemoveOldest@DBOCache@@MAEPAVDBObj@@XZ

        // DBOCache.GetFreeObj:
        public DBObj* GetFreeObj() => ((delegate* unmanaged[Thiscall]<ref DBOCache, DBObj*>)0x00416A70)(ref this); // .text:004167D0 ; DBObj *__thiscall DBOCache::GetFreeObj(DBOCache *this) .text:004167D0 ?GetFreeObj@DBOCache@@UAEPAVDBObj@@XZ

        // DBOCache.GetIfInMemory:
        public DBObj* GetIfInMemory(Byte a1) => ((delegate* unmanaged[Thiscall]<ref DBOCache, Byte, DBObj*>)0x00416EB0)(ref this, a1); // .text:00416C60 ; public: virtual class DBObj * __thiscall DBOCache::GetIfInMemory(class IDClass<struct _tagDataID, 32, 0>, bool) .text:00416C60 ?GetIfInMemory@DBOCache@@UAEPAVDBObj@@V?$IDClass@U_tagDataID@@$0CA@$0A@@@_N@Z

        // DBOCache.GetIfUsing:
        public DBObj* GetIfUsing(UInt32 id) => ((delegate* unmanaged[Thiscall]<ref DBOCache, UInt32, DBObj*>)0x00416F40)(ref this, id); // .text:00416CF0 ; DBObj *__thiscall DBOCache::GetIfUsing(DBOCache *this, IDClass<_tagDataID,32,0> id) .text:00416CF0 ?GetIfUsing@DBOCache@@QAEPAVDBObj@@V?$IDClass@U_tagDataID@@$0CA@$0A@@@@Z

        // DBOCache.IsInMemory:
        public Byte IsInMemory(UInt32 did) => ((delegate* unmanaged[Thiscall]<ref DBOCache, UInt32, Byte>)0x00416C50)(ref this, did); // .text:00416A00 ; bool __thiscall DBOCache::IsInMemory(DBOCache *this, IDClass<_tagDataID,32,0> did) .text:00416A00 ?IsInMemory@DBOCache@@QAE_NV?$IDClass@U_tagDataID@@$0CA@$0A@@@@Z

        // DBOCache.KeepFreeObjects:
        public Byte KeepFreeObjects(Byte keep_f) => ((delegate* unmanaged[Thiscall]<ref DBOCache, Byte, Byte>)0x00416970)(ref this, keep_f); // .text:004166D0 ; bool __thiscall DBOCache::KeepFreeObjects(DBOCache *this, bool keep_f) .text:004166D0 ?KeepFreeObjects@DBOCache@@QAE_N_N@Z

        // DBOCache.Release:
        public UInt32 Release(UInt32 id) => ((delegate* unmanaged[Thiscall]<ref DBOCache, UInt32, UInt32>)0x00416FA0)(ref this, id); // .text:00416D50 ; unsigned int __thiscall DBOCache::Release(DBOCache *this, IDClass<_tagDataID,32,0> id) .text:00416D50 ?Release@DBOCache@@UAEKV?$IDClass@U_tagDataID@@$0CA@$0A@@@@Z

        // DBOCache.ReloadObject:
        public Byte ReloadObject(UInt32 id) => ((delegate* unmanaged[Thiscall]<ref DBOCache, UInt32, Byte>)0x00416B30)(ref this, id); // .text:00416890 ; bool __thiscall DBOCache::ReloadObject(DBOCache *this, IDClass<_tagDataID,32,0> id) .text:00416890 ?ReloadObject@DBOCache@@UAE_NV?$IDClass@U_tagDataID@@$0CA@$0A@@@@Z

        // DBOCache.RemoveObj:
        public Byte RemoveObj(UInt32 did) => ((delegate* unmanaged[Thiscall]<ref DBOCache, UInt32, Byte>)0x00416E40)(ref this, did); // .text:00416BF0 ; bool __thiscall DBOCache::RemoveObj(DBOCache *this, IDClass<_tagDataID,32,0> did) .text:00416BF0 ?RemoveObj@DBOCache@@UAE_NV?$IDClass@U_tagDataID@@$0CA@$0A@@@@Z

        // DBOCache.RemoveObj_Internal:
        // public void RemoveObj_Internal(DBObj* object_p) => ((delegate* unmanaged[Thiscall]<ref DBOCache, DBObj*, void>)0xDEADBEEF)(ref this, object_p); // .text:004171B0 ; void __thiscall DBOCache::RemoveObj_Internal(DBOCache *this, DBObj *object_p) .text:004171B0 ?RemoveObj_Internal@DBOCache@@MAEXPAVDBObj@@@Z

        // DBOCache.UseTime:
        public void UseTime() => ((delegate* unmanaged[Thiscall]<ref DBOCache, void>)0x00416AC0)(ref this); // .text:00416820 ; void __thiscall DBOCache::UseTime(DBOCache *this) .text:00416820 ?UseTime@DBOCache@@QAEXXZ
    }
    public unsafe struct _Collection {
    }

    public unsafe struct PreprocHeader {
        // Struct:
        public PStringBase<Char> header_data_0;
        public PStringBase<Char> header_data_1;
        public PStringBase<Char> header_data_2;
        public PStringBase<Char> header_data_3;
        public override string ToString() => $"{header_data_0},{header_data_1},{header_data_2},{header_data_3}";
    }
    public unsafe struct FreelistDef {
        // Struct:
        public Byte m_bRecycle;
        public Byte m_bShrink;
        public UInt32 m_nIdealSize;
        public UInt32 m_nMaxSize;
        public override string ToString() => $"m_bRecycle:{m_bRecycle:X2}, m_bShrink:{m_bShrink:X2}, m_nIdealSize:{m_nIdealSize:X8}, m_nMaxSize:{m_nMaxSize:X8}";

    }
    public unsafe struct IDataGraph {
        // Struct:
        public IDataGraph.Vtbl* vfptr;
        public override string ToString() => $"vfptr:->(IDataGraph.Vtbl*)0x{(int)vfptr:X8}";
        public unsafe struct Vtbl {
            public static delegate* unmanaged[Thiscall]<IDataGraph*, UInt32, Byte> add_did; // bool (__thiscall *add_did)(IDataGraph *this, IDClass<_tagDataID,32,0>);
            public fixed byte gap4[4];
            public static delegate* unmanaged[Thiscall]<IDataGraph*, UInt32, Byte> remove_did; // bool (__thiscall *remove_did)(IDataGraph *this, IDClass<_tagDataID,32,0>);
            public static delegate* unmanaged[Thiscall]<IDataGraph*, UInt32, Byte> add_iid; // bool (__thiscall *add_iid)(IDataGraph *this, unsigned int);
            public static delegate* unmanaged[Thiscall]<IDataGraph*, UInt32, UInt32, Byte> add_iid_iid_link; // bool (__thiscall *add_iid_iid_link)(IDataGraph *this, unsigned int, unsigned int);
            public static delegate* unmanaged[Thiscall]<IDataGraph*, UInt32, UInt32, Byte> add_iid_did_link; // bool (__thiscall *add_iid_did_link)(IDataGraph *this, unsigned int, IDClass<_tagDataID,32,0>);
            public static delegate* unmanaged[Thiscall]<IDataGraph*, UInt32, Byte> remove_iid; // bool (__thiscall *remove_iid)(IDataGraph *this, unsigned int);
            public static delegate* unmanaged[Thiscall]<IDataGraph*, DataKey*, Byte> add; // bool (__thiscall *add)(IDataGraph *this, struct DataKey *);
            public static delegate* unmanaged[Thiscall]<IDataGraph*, DataKey*, DataKey*, Byte> add_link; // bool (__thiscall *add_link)(IDataGraph *this, struct DataKey *, struct DataKey *);
            public static delegate* unmanaged[Thiscall]<IDataGraph*, DataKey*, Byte> remove; // bool (__thiscall *remove)(IDataGraph *this, struct DataKey *);
            public static delegate* unmanaged[Thiscall]<IDataGraph*, DataKey*, DataKey*, Byte> remove_link; // bool (__thiscall *remove_link)(IDataGraph *this, struct DataKey *, struct DataKey *);
            public static delegate* unmanaged[Thiscall]<IDataGraph*, UInt32, UInt32, Byte> check_did_link; // bool (__thiscall *check_did_link)(IDataGraph *this, IDClass<_tagDataID,32,0>, IDClass<_tagDataID,32,0>);
            public static delegate* unmanaged[Thiscall]<IDataGraph*, UInt32, UInt32, Byte> check_iid_link; // bool (__thiscall *check_iid_link)(IDataGraph *this, unsigned int, unsigned int);
            public static delegate* unmanaged[Thiscall]<IDataGraph*, UInt32, UInt32, Byte> check_iid_did_link; // bool (__thiscall *check_iid_did_link)(IDataGraph *this, unsigned int, IDClass<_tagDataID,32,0>);
            public static delegate* unmanaged[Thiscall]<IDataGraph*, UInt32, UInt32, Byte> check_ancestor_did; // bool (__thiscall *check_ancestor_did)(IDataGraph *this, IDClass<_tagDataID,32,0>, IDClass<_tagDataID,32,0>);
        }
    }
    public unsafe struct DataKey {
    }



    public unsafe struct Archive {
        // Struct:
        public Archive.Vtbl* vfptr;
        public UInt32 m_flags;
        public TResult m_hrError;
        public SmartBuffer m_buffer;
        public UInt32 m_currOffset;
        public HashTable<UInt32, PTR<Interface>>* m_pcUserDataHash;
        public IArchiveVersionStack* m_pVersionStack;
        public override string ToString() => $"vfptr:->(Archive.Vtbl*)0x{(int)vfptr:X8}, m_flags:{m_flags:X8}, m_hrError(TResult):{m_hrError}, m_buffer(SmartBuffer):{m_buffer}, m_currOffset:{m_currOffset:X8}, m_pcUserDataHash:->(HashTable<UInt32,Interface*,0>*)0x{(int)m_pcUserDataHash:X8}, m_pVersionStack:->(IArchiveVersionStack*)0x{(int)m_pVersionStack:X8}";
        public unsafe struct Vtbl {
            public static delegate* unmanaged[Thiscall]<Archive*, ArchiveInitializer*, SmartBuffer*, void> InitForPacking; // void (__thiscall *InitForPacking)(Archive *this, ArchiveInitializer *, SmartBuffer *);
            public static delegate* unmanaged[Thiscall]<Archive*, ArchiveInitializer*, SmartBuffer*, void> InitForUnpacking; // void (__thiscall *InitForUnpacking)(Archive *this, ArchiveInitializer *, SmartBuffer *);
            public static delegate* unmanaged[Thiscall]<Archive*, Byte, void> SetCheckpointing; // void (__thiscall *SetCheckpointing)(Archive *this, bool);
            public static delegate* unmanaged[Thiscall]<Archive*, void> InitVersionStack; // void (__thiscall *InitVersionStack)(Archive *this);
            public static delegate* unmanaged[Thiscall]<Archive*, void> CreateVersionStack; // void (__thiscall *CreateVersionStack)(Archive *this);
        }
        public unsafe struct tagSetCurrentCoreVersion {
            public ArchiveInitializer a0;
            public override string ToString() => $"a0(ArchiveInitializer):{a0}";
        }
        public unsafe struct SetVersionRow {
            public ArchiveInitializer a0;
            public ArchiveVersionRow* m_rInitialData;
            public override string ToString() => $"a0(ArchiveInitializer):{a0}, m_rInitialData:->(ArchiveVersionRow*)0x{(int)m_rInitialData:X8}";
        }
        // Enums:
        public enum tagUnpacking : UInt32 {
            Unpacking = 0x0,
        };
        public enum tagPacking : UInt32 {
            Packing = 0x0,
        };

        // Functions:

        // Archive.CheckAlignment:
        public void CheckAlignment(UInt32 i_objectSize) => ((delegate* unmanaged[Thiscall]<ref Archive, UInt32, void>)0x0040AD10)(ref this, i_objectSize); // .text:0040A9B0 ; void __thiscall Archive::CheckAlignment(Archive *this, unsigned int i_objectSize) .text:0040A9B0 ?CheckAlignment@Archive@@QAEXI@Z

        // Archive.CreateVersionStack:
        public void CreateVersionStack() => ((delegate* unmanaged[Thiscall]<ref Archive, void>)0x0040AEF0)(ref this); // .text:0040AB90 ; void __thiscall Archive::CreateVersionStack(Archive *this) .text:0040AB90 ?CreateVersionStack@Archive@@MAEXXZ

        // Archive.GetBytes:
        public char* GetBytes(UInt32 i_size) => ((delegate* unmanaged[Thiscall]<ref Archive, UInt32, char*>)0x0040ACF0)(ref this, i_size); // .text:0040A990 ; char *__thiscall Archive::GetBytes(Archive *this, unsigned int i_size) .text:0040A990 ?GetBytes@Archive@@QAEPAEI@Z

        // Archive.GetRemainingBuffer:
        public SmartBuffer* GetRemainingBuffer(SmartBuffer* result) => ((delegate* unmanaged[Thiscall]<ref Archive, SmartBuffer*, SmartBuffer*>)0x0040A920)(ref this, result); // .text:0040A5C0 ; SmartBuffer *__thiscall Archive::GetRemainingBuffer(Archive *this, SmartBuffer *result) .text:0040A5C0 ?GetRemainingBuffer@Archive@@QAE?AVSmartBuffer@@XZ

        // Archive.GetSerializedBuffer:
        public SmartBuffer* GetSerializedBuffer(SmartBuffer* result) => ((delegate* unmanaged[Thiscall]<ref Archive, SmartBuffer*, SmartBuffer*>)0x0040A900)(ref this, result); // .text:0040A5A0 ; SmartBuffer *__thiscall Archive::GetSerializedBuffer(Archive *this, SmartBuffer *result) .text:0040A5A0 ?GetSerializedBuffer@Archive@@QAE?AVSmartBuffer@@XZ

        // Archive.GetSizeLeft:
        public UInt32 GetSizeLeft() => ((delegate* unmanaged[Thiscall]<ref Archive, UInt32>)0x0040A8F0)(ref this); // .text:0040A590 ; unsigned int __thiscall Archive::GetSizeLeft(Archive *this) .text:0040A590 ?GetSizeLeft@Archive@@QBEIXZ

        // Archive.GetSizeUsed:
        public UInt32 GetSizeUsed() => ((delegate* unmanaged[Thiscall]<ref Archive, UInt32>)0x0040A8D0)(ref this); // .text:0040A570 ; unsigned int __thiscall Archive::GetSizeUsed(Archive *this) .text:0040A570 ?GetSizeUsed@Archive@@QBEIXZ

        // Archive.GetVersionByToken:
        public UInt32 GetVersionByToken(UInt32 i_tokVersion) => ((delegate* unmanaged[Thiscall]<ref Archive, UInt32, UInt32>)0x0040A960)(ref this, i_tokVersion); // .text:0040A600 ; unsigned int __thiscall Archive::GetVersionByToken(Archive *this, unsigned int i_tokVersion) .text:0040A600 ?GetVersionByToken@Archive@@QBEKK@Z

        // Archive.GetVersionRowByHandle:
        public Byte GetVersionRowByHandle(UInt32 i_hVersion, ArchiveVersionRow** o_pVersionRow) => ((delegate* unmanaged[Thiscall]<ref Archive, UInt32, ArchiveVersionRow**, Byte>)0x0040AB90)(ref this, i_hVersion, o_pVersionRow); // .text:0040A830 ; bool __thiscall Archive::GetVersionRowByHandle(Archive *this, IDClass<_tagVersionHandle,32,0> i_hVersion, ArchiveVersionRow **o_pVersionRow) .text:0040A830 ?GetVersionRowByHandle@Archive@@QAE_NV?$IDClass@U_tagVersionHandle@@$0CA@$0A@@@AAPBVArchiveVersionRow@@@Z

        // Archive.InitForPacking:
        public void InitForPacking(ArchiveInitializer* i_rInitializer, SmartBuffer* i_buffer) => ((delegate* unmanaged[Thiscall]<ref Archive, ArchiveInitializer*, SmartBuffer*, void>)0x0040AFB0)(ref this, i_rInitializer, i_buffer); // .text:0040AC50 ; void __thiscall Archive::InitForPacking(Archive *this, ArchiveInitializer *i_rInitializer, SmartBuffer *i_buffer) .text:0040AC50 ?InitForPacking@Archive@@UAEXABVArchiveInitializer@@ABVSmartBuffer@@@Z

        // Archive.InitForUnpacking:
        public void InitForUnpacking(ArchiveInitializer* i_rInitializer, SmartBuffer* i_buffer) => ((delegate* unmanaged[Thiscall]<ref Archive, ArchiveInitializer*, SmartBuffer*, void>)0x0040B020)(ref this, i_rInitializer, i_buffer); // .text:0040ACC0 ; void __thiscall Archive::InitForUnpacking(Archive *this, ArchiveInitializer *i_rInitializer, SmartBuffer *i_buffer) .text:0040ACC0 ?InitForUnpacking@Archive@@UAEXABVArchiveInitializer@@ABVSmartBuffer@@@Z

        // Archive.InitVersionStack:
        public void InitVersionStack() => ((delegate* unmanaged[Thiscall]<ref Archive, void>)0x0040A940)(ref this); // .text:0040A5E0 ; void __thiscall Archive::InitVersionStack(Archive *this) .text:0040A5E0 ?InitVersionStack@Archive@@MAEXXZ

        // Archive.IsWordAligned:
        public Byte IsWordAligned() => ((delegate* unmanaged[Thiscall]<ref Archive, Byte>)0x0040AA60)(ref this); // .text:0040A700 ; bool __thiscall Archive::IsWordAligned(Archive *this) .text:0040A700 ?IsWordAligned@Archive@@QBE_NXZ

        // Archive.PeekBytes:
        public char* PeekBytes(UInt32 i_position, UInt32 i_size) => ((delegate* unmanaged[Thiscall]<ref Archive, UInt32, UInt32, char*>)0x0040AC70)(ref this, i_position, i_size); // .text:0040A910 ; char *__thiscall Archive::PeekBytes(Archive *this, unsigned int i_position, unsigned int i_size) .text:0040A910 ?PeekBytes@Archive@@QAEPAEII@Z

        // Archive.PushVersionRow:
        public UInt32* PushVersionRow(UInt32* result, ArchiveVersionRow* i_rInitialData) => ((delegate* unmanaged[Thiscall]<ref Archive, UInt32*, ArchiveVersionRow*, UInt32*>)0x0040AB30)(ref this, result, i_rInitialData); // .text:0040A7D0 ; IDClass<_tagVersionHandle,32,0> *__thiscall Archive::PushVersionRow(Archive *this, IDClass<_tagVersionHandle,32,0> *result, ArchiveVersionRow *i_rInitialData) .text:0040A7D0 ?PushVersionRow@Archive@@QAE?AV?$IDClass@U_tagVersionHandle@@$0CA@$0A@@@ABVArchiveVersionRow@@@Z

        // Archive.PushVersionRow:
        public UInt32* PushVersionRow(UInt32* result) => ((delegate* unmanaged[Thiscall]<ref Archive, UInt32*, UInt32*>)0x0040AAD0)(ref this, result); // .text:0040A770 ; IDClass<_tagVersionHandle,32,0> *__thiscall Archive::PushVersionRow(Archive *this, IDClass<_tagVersionHandle,32,0> *result) .text:0040A770 ?PushVersionRow@Archive@@QAE?AV?$IDClass@U_tagVersionHandle@@$0CA@$0A@@@XZ

        // Archive.RaiseError:
        public void RaiseError() => ((delegate* unmanaged[Thiscall]<ref Archive, void>)0x0040AA50)(ref this); // .text:0040A6F0 ; void __thiscall Archive::RaiseError(Archive *this) .text:0040A6F0 ?RaiseError@Archive@@QAEXXZ

        // Archive.ReleaseUserData:
        public void ReleaseUserData() => ((delegate* unmanaged[Thiscall]<ref Archive, void>)0x0040AF20)(ref this); // .text:0040ABC0 ; void __thiscall Archive::ReleaseUserData(Archive *this) .text:0040ABC0 ?ReleaseUserData@Archive@@IAEXXZ

        // Archive.SetCheckpointing:
        public void SetCheckpointing(Byte _checkpointing) => ((delegate* unmanaged[Thiscall]<ref Archive, Byte, void>)0x0040A9D0)(ref this, _checkpointing); // .text:0040A670 ; void __thiscall Archive::SetCheckpointing(Archive *this, bool _checkpointing) .text:0040A670 ?SetCheckpointing@Archive@@UAEX_N@Z

        // Archive.SetCurrentPosition:
        public void SetCurrentPosition(UInt32 i_position) => ((delegate* unmanaged[Thiscall]<ref Archive, UInt32, void>)0x0040A8E0)(ref this, i_position); // .text:0040A580 ; void __thiscall Archive::SetCurrentPosition(Archive *this, unsigned int i_position) .text:0040A580 ?SetCurrentPosition@Archive@@QAEXI@Z

        // Archive.SetDBLoader:
        public void SetDBLoader(Byte _using_DBLoader) => ((delegate* unmanaged[Thiscall]<ref Archive, Byte, void>)0x0040AA10)(ref this, _using_DBLoader); // .text:0040A6B0 ; void __thiscall Archive::SetDBLoader(Archive *this, bool _using_DBLoader) .text:0040A6B0 ?SetDBLoader@Archive@@QAEX_N@Z

        // Archive.SetVersionByToken:
        public Byte SetVersionByToken(UInt32 i_tokVersion, UInt32 i_iVersion) => ((delegate* unmanaged[Thiscall]<ref Archive, UInt32, UInt32, Byte>)0x0040AA70)(ref this, i_tokVersion, i_iVersion); // .text:0040A710 ; bool __thiscall Archive::SetVersionByToken(Archive *this, unsigned int i_tokVersion, unsigned int i_iVersion) .text:0040A710 ?SetVersionByToken@Archive@@QAE_NKK@Z

        // Archive.SetWordAligned:
        public void SetWordAligned(Byte _aligned) => ((delegate* unmanaged[Thiscall]<ref Archive, Byte, void>)0x0040AA30)(ref this, _aligned); // .text:0040A6D0 ; void __thiscall Archive::SetWordAligned(Archive *this, bool _aligned) .text:0040A6D0 ?SetWordAligned@Archive@@QAEX_N@Z

        // Archive.UsingDBLoader:
        public Byte UsingDBLoader() => ((delegate* unmanaged[Thiscall]<ref Archive, Byte>)0x0040A9F0)(ref this); // .text:0040A690 ; bool __thiscall Archive::UsingDBLoader(Archive *this) .text:0040A690 ?UsingDBLoader@Archive@@QBE_NXZ

        // Globals:
        public static Archive.tagSetCurrentCoreVersion* SetCurrentCoreVersion = (Archive.tagSetCurrentCoreVersion*)0x008183B8; // .data:008173B8 ; Archive::tagSetCurrentCoreVersion Archive::SetCurrentCoreVersion .data:008173B8 ?SetCurrentCoreVersion@Archive@@2VtagSetCurrentCoreVersion@1@A
    }

    public unsafe struct IArchiveVersionStack {
        public Interface a0;
        public override string ToString() => a0.ToString();
    };
    public unsafe struct SmartBuffer {
        // Struct:
        public UInt32 m_startOffset;
        public UInt32 m_size;
        public GrowBuffer* m_masterBuffer;
        public override string ToString() => $"m_startOffset:{m_startOffset:X8}, m_size:{m_size:X8}, m_masterBuffer:->(GrowBuffer*)0x{(int)m_masterBuffer:X8}";

        // Functions:

        // SmartBuffer.__Ctor:
        public void __Ctor(SmartBuffer* i_rhs) => ((delegate* unmanaged[Thiscall]<ref SmartBuffer, SmartBuffer*, void>)0x00406F60)(ref this, i_rhs); // .text:00406C60 ; void __thiscall SmartBuffer::SmartBuffer(SmartBuffer *this, SmartBuffer *i_rhs) .text:00406C60 ??0SmartBuffer@@QAE@ABV0@@Z

        // SmartBuffer.__Ctor:
        public void __Ctor(void* i_addr, UInt32 i_size) => ((delegate* unmanaged[Thiscall]<ref SmartBuffer, void*, UInt32, void>)0x00407060)(ref this, i_addr, i_size); // .text:00406D60 ; void __thiscall SmartBuffer::SmartBuffer(SmartBuffer *this, void *i_addr, unsigned int i_size) .text:00406D60 ??0SmartBuffer@@QAE@PAXI@Z

        // SmartBuffer.__Ctor:
        public void __Ctor() => ((delegate* unmanaged[Thiscall]<ref SmartBuffer, void>)0x00406D60)(ref this); // .text:00406A60 ; void __thiscall SmartBuffer::SmartBuffer(SmartBuffer *this) .text:00406A60 ??0SmartBuffer@@QAE@XZ

        // SmartBuffer.operator_equals:
        public SmartBuffer* operator_equals() => ((delegate* unmanaged[Thiscall]<ref SmartBuffer, SmartBuffer*>)0x004070D0)(ref this); // .text:00406DD0 ; public: class SmartBuffer & __thiscall SmartBuffer::operator=(class SmartBuffer const &) .text:00406DD0 ??4SmartBuffer@@QAEAAV0@ABV0@@Z

        // SmartBuffer.Borrow:
        public void Borrow(char* i_addr, UInt32 i_size) => ((delegate* unmanaged[Thiscall]<ref SmartBuffer, char*, UInt32, void>)0x004073B0)(ref this, i_addr, i_size); // .text:004070B0 ; void __thiscall SmartBuffer::Borrow(SmartBuffer *this, char *i_addr, unsigned int i_size) .text:004070B0 ?Borrow@SmartBuffer@@QAEXPAEI@Z

        // SmartBuffer.CanGrow:
        public Byte CanGrow() => ((delegate* unmanaged[Thiscall]<ref SmartBuffer, Byte>)0x00406D70)(ref this); // .text:00406A70 ; bool __thiscall SmartBuffer::CanGrow(SmartBuffer *this) .text:00406A70 ?CanGrow@SmartBuffer@@QBE_NXZ

        // SmartBuffer.Clone:
        public SmartBuffer* Clone(SmartBuffer* result) => ((delegate* unmanaged[Thiscall]<ref SmartBuffer, SmartBuffer*, SmartBuffer*>)0x004073F0)(ref this, result); // .text:004070F0 ; SmartBuffer *__thiscall SmartBuffer::Clone(SmartBuffer *this, SmartBuffer *result) .text:004070F0 ?Clone@SmartBuffer@@QBE?AV1@XZ

        // SmartBuffer.CreateNewMasterBuffer:
        public void CreateNewMasterBuffer() => ((delegate* unmanaged[Thiscall]<ref SmartBuffer, void>)0x004071E0)(ref this); // .text:00406EE0 ; void __thiscall SmartBuffer::CreateNewMasterBuffer(SmartBuffer *this) .text:00406EE0 ?CreateNewMasterBuffer@SmartBuffer@@QAEXXZ

        // SmartBuffer.GetBuffer:
        public char* GetBuffer() => ((delegate* unmanaged[Thiscall]<ref SmartBuffer, char*>)0x00406D80)(ref this); // .text:00406A80 ; const char *__thiscall SmartBuffer::GetBuffer(SmartBuffer *this) .text:00406A80 ?GetBuffer@SmartBuffer@@QAEPAEXZ

        // SmartBuffer.GetShareCount:
        public UInt32 GetShareCount() => ((delegate* unmanaged[Thiscall]<ref SmartBuffer, UInt32>)0x00406DC0)(ref this); // .text:00406AC0 ; unsigned int __thiscall SmartBuffer::GetShareCount(SmartBuffer *this) .text:00406AC0 ?GetShareCount@SmartBuffer@@QBEKXZ

        // SmartBuffer.GetSize:
        public UInt32 GetSize() => ((delegate* unmanaged[Thiscall]<ref SmartBuffer, UInt32>)0x00406DB0)(ref this); // .text:00406AB0 ; unsigned int __thiscall SmartBuffer::GetSize(SmartBuffer *this) .text:00406AB0 ?GetSize@SmartBuffer@@QBEIXZ

        // SmartBuffer.MakeWindow:
        public SmartBuffer* MakeWindow(SmartBuffer* result, UInt32 i_start) => ((delegate* unmanaged[Thiscall]<ref SmartBuffer, SmartBuffer*, UInt32, SmartBuffer*>)0x00407390)(ref this, result, i_start); // .text:00407090 ; SmartBuffer *__thiscall SmartBuffer::MakeWindow(SmartBuffer *this, SmartBuffer *result, unsigned int i_start) .text:00407090 ?MakeWindow@SmartBuffer@@QAE?AV1@I@Z

        // SmartBuffer.MakeWindow:
        public SmartBuffer* MakeWindow(SmartBuffer* result, UInt32 i_start, UInt32 i_size) => ((delegate* unmanaged[Thiscall]<ref SmartBuffer, SmartBuffer*, UInt32, UInt32, SmartBuffer*>)0x00407140)(ref this, result, i_start, i_size); // .text:00406E40 ; SmartBuffer *__thiscall SmartBuffer::MakeWindow(SmartBuffer *this, SmartBuffer *result, unsigned int i_start, unsigned int i_size) .text:00406E40 ?MakeWindow@SmartBuffer@@QAE?AV1@II@Z

        // SmartBuffer.Orphan:
        public char* Orphan() => ((delegate* unmanaged[Thiscall]<ref SmartBuffer, char*>)0x00406D90)(ref this); // .text:00406A90 ; char *__thiscall SmartBuffer::Orphan(SmartBuffer *this) .text:00406A90 ?Orphan@SmartBuffer@@QAEPAEXZ

        // SmartBuffer.ReconfigureAllocation:
        public void ReconfigureAllocation(UInt32 i_sizeNeeded, UInt32 i_dwBehaviorBits) => ((delegate* unmanaged[Thiscall]<ref SmartBuffer, UInt32, UInt32, void>)0x004074B0)(ref this, i_sizeNeeded, i_dwBehaviorBits); // .text:004071B0 ; void __thiscall SmartBuffer::ReconfigureAllocation(SmartBuffer *this, unsigned int i_sizeNeeded, unsigned int i_dwBehaviorBits) .text:004071B0 ?ReconfigureAllocation@SmartBuffer@@QAEXIK@Z

        // SmartBuffer.ReleaseMasterBuffer:
        public void ReleaseMasterBuffer() => ((delegate* unmanaged[Thiscall]<ref SmartBuffer, void>)0x00406F90)(ref this); // .text:00406C90 ; void __thiscall SmartBuffer::ReleaseMasterBuffer(SmartBuffer *this) .text:00406C90 ?ReleaseMasterBuffer@SmartBuffer@@QAEXXZ
    }
    public unsafe struct GrowBuffer {
        // Struct:
        public Turbine_RefCount a0;
        public char* m_data;
        public UInt32 m_size;
        public Byte m_ownsBuffer;
        public Byte m_bCanResize;
        public Byte m_bAllocateFromFreelist;
        public override string ToString() => $"a0(Turbine_RefCount):{a0}, m_data:->(char*)0x{(int)m_data:X8}, m_size:{m_size:X8}, m_ownsBuffer:{m_ownsBuffer:X2}, m_bCanResize:{m_bCanResize:X2}, m_bAllocateFromFreelist:{m_bAllocateFromFreelist:X2}";
        public unsafe struct FreeGrowBuffer {
            public char* pData;
            public UInt32 cbSize;
            public override string ToString() => $"pData:->(char*)0x{(int)pData:X8}, cbSize:{cbSize:X8}";
        }

        // Functions:

        // GrowBuffer.FreeBuffer:
        public void FreeBuffer() => ((delegate* unmanaged[Thiscall]<ref GrowBuffer, void>)0x00406E80)(ref this); // .text:00406B80 ; void __thiscall GrowBuffer::FreeBuffer(GrowBuffer *this) .text:00406B80 ?FreeBuffer@GrowBuffer@@AAEXXZ

        // GrowBuffer.GetGoodSize:
        public UInt32 GetGoodSize(UInt32 i_sizeNeeded) => ((delegate* unmanaged[Thiscall]<ref GrowBuffer, UInt32, UInt32>)0x00406E20)(ref this, i_sizeNeeded); // .text:00406B20 ; unsigned int __thiscall GrowBuffer::GetGoodSize(GrowBuffer *this, unsigned int i_sizeNeeded) .text:00406B20 ?GetGoodSize@GrowBuffer@@QAEII@Z

        // GrowBuffer.GrowExact:
        public void GrowExact(UInt32 i_exactSize) => ((delegate* unmanaged[Thiscall]<ref GrowBuffer, UInt32, void>)0x00407250)(ref this, i_exactSize); // .text:00406F50 ; void __thiscall GrowBuffer::GrowExact(GrowBuffer *this, unsigned int i_exactSize) .text:00406F50 ?GrowExact@GrowBuffer@@QAEXI@Z

        // Globals:
        public static CSpinLock* m_pFreeListLock = *(CSpinLock**)0x00837798; // .data:00836798 ; CSpinLock<1048576,0> *GrowBuffer::m_pFreeListLock .data:00836798 ?m_pFreeListLock@GrowBuffer@@0PAV?$CSpinLock@$0BAAAAA@$0A@@@A
        public static UInt32* m_nFreeListEntries = (UInt32*)0x0083779C; // .data:0083679C ; unsigned int GrowBuffer::m_nFreeListEntries .data:0083679C ?m_nFreeListEntries@GrowBuffer@@0KA
        //public static `void __thiscall* GrowExact(unsigned int)'.`3'.`local static guard'{3}' = (`void __thiscall*)0x008377A0; // .data:008367A0 ; `public: void __thiscall GrowBuffer::GrowExact(unsigned int)'::`3'::`local static guard'{3}' .data:008367A0 ??_B?2??GrowExact@GrowBuffer@@QAEXI@Z@52
        public static GrowBuffer.FreeGrowBuffer** m_FreeList = (GrowBuffer.FreeGrowBuffer**)0x00837758; // .data:00836758 ; GrowBuffer::FreeGrowBuffer GrowBuffer::m_FreeList[8] .data:00836758 ?m_FreeList@GrowBuffer@@0PAUFreeGrowBuffer@1@A
    }
    public unsafe struct ArchiveVersionRow {
        // Struct:
        public ArchiveVersionRow.Vtbl* vfptr;
        public PrimitiveInplaceArray<ArchiveVersionRow.VersionEntry> m_aVersions;
        public override string ToString() => $"vfptr:->(ArchiveVersionRow.Vtbl*)0x{(int)vfptr:X8}, m_aVersions(PrimitiveInplaceArray<ArchiveVersionRow.VersionEntry,8,1>):{m_aVersions}";
        public unsafe struct VersionEntry {
            public UInt32 tokVersion;
            public UInt32 iVersion;
            public override string ToString() => $"tokVersion:{tokVersion:X8}, iVersion:{iVersion:X8}";
        }
        public unsafe struct Vtbl {
            public static delegate* unmanaged[Thiscall]<ArchiveVersionRow*, UInt32, UInt32> GetVersionByToken; // unsigned int (__thiscall *GetVersionByToken)(ArchiveVersionRow *this, unsigned int);
        }

        // Functions:

        // ArchiveVersionRow.GetVersionByToken:
        public UInt32 GetVersionByToken(UInt32 i_tokVersion) => ((delegate* unmanaged[Thiscall]<ref ArchiveVersionRow, UInt32, UInt32>)0x004103B0)(ref this, i_tokVersion); // .text:004100F0 ; unsigned int __thiscall ArchiveVersionRow::GetVersionByToken(ArchiveVersionRow *this, unsigned int i_tokVersion) .text:004100F0 ?GetVersionByToken@ArchiveVersionRow@@UBEKK@Z

        // ArchiveVersionRow.SerializeFooter:
        public Byte SerializeFooter(UInt32 i_hSerialize, Archive* io_rcArchive) => ((delegate* unmanaged[Thiscall]<ref ArchiveVersionRow, UInt32, Archive*, Byte>)0x004106F0)(ref this, i_hSerialize, io_rcArchive); // .text:00410430 ; bool __thiscall ArchiveVersionRow::SerializeFooter(ArchiveVersionRow *this, unsigned int i_hSerialize, Archive *io_rcArchive) .text:00410430 ?SerializeFooter@ArchiveVersionRow@@QAE_NKAAVArchive@@@Z

        // ArchiveVersionRow.SerializeHeader:
        public UInt32 SerializeHeader(Archive* io_rcArchive) => ((delegate* unmanaged[Thiscall]<ref ArchiveVersionRow, Archive*, UInt32>)0x00410630)(ref this, io_rcArchive); // .text:00410370 ; unsigned int __thiscall ArchiveVersionRow::SerializeHeader(ArchiveVersionRow *this, Archive *io_rcArchive) .text:00410370 ?SerializeHeader@ArchiveVersionRow@@QAEKAAVArchive@@@Z

        // ArchiveVersionRow.SerializeRow:
        public void SerializeRow(Archive* io_rcArchive) => ((delegate* unmanaged[Thiscall]<ref ArchiveVersionRow, Archive*, void>)0x00410590)(ref this, io_rcArchive); // .text:004102D0 ; void __thiscall ArchiveVersionRow::SerializeRow(ArchiveVersionRow *this, Archive *io_rcArchive) .text:004102D0 ?SerializeRow@ArchiveVersionRow@@IAEXAAVArchive@@@Z

        // ArchiveVersionRow.SetVersion:
        public Byte SetVersion(UInt32 i_tokVersion, UInt32 i_iVersion) => ((delegate* unmanaged[Thiscall]<ref ArchiveVersionRow, UInt32, UInt32, Byte>)0x00410500)(ref this, i_tokVersion, i_iVersion); // .text:00410240 ; bool __thiscall ArchiveVersionRow::SetVersion(ArchiveVersionRow *this, unsigned int i_tokVersion, unsigned int i_iVersion) .text:00410240 ?SetVersion@ArchiveVersionRow@@QAE_NKK@Z
    }
    public unsafe struct AutoStoreVersionArchive {
        // Struct:
        public Archive a0;
        public AutoStoreVersionArchive.tagSerializeVersionRow m_SerializeVersionRow;
        public Byte m_bOnSerializingDoneCalled;
        public override string ToString() => $"a0(Archive):{a0}, m_SerializeVersionRow(AutoStoreVersionArchive.tagSerializeVersionRow):{m_SerializeVersionRow}, m_bOnSerializingDoneCalled:{m_bOnSerializingDoneCalled:X2}";
        public unsafe struct tagSerializeVersionRow {
            public ArchiveInitializer a0;
            public UInt32 m_hSerialize;
            public UInt32 m_hVersionRow;
            public ArchiveVersionRow m_rowInitialData;
            public override string ToString() => $"a0(ArchiveInitializer):{a0}, m_hSerialize:{m_hSerialize:X8}, m_hVersionRow(IDClass<_tagVersionHandle,32,0>):{m_hVersionRow}, m_rowInitialData(ArchiveVersionRow):{m_rowInitialData}";
        }

        // Functions:

        // AutoStoreVersionArchive.__Ctor:
        // public void __Ctor(Archive.tagUnpacking __formal, SmartBuffer* buff) => ((delegate* unmanaged[Thiscall]<ref AutoStoreVersionArchive, Archive.tagUnpacking, SmartBuffer*, void>)0xDEADBEEF)(ref this, __formal, buff); // .text:00446780 ; void __thiscall AutoStoreVersionArchive::AutoStoreVersionArchive(AutoStoreVersionArchive *this, Archive::tagUnpacking __formal, SmartBuffer *buff) .text:00446780 ??0AutoStoreVersionArchive@@QAE@W4tagUnpacking@Archive@@ABVSmartBuffer@@@Z

        // AutoStoreVersionArchive.__Ctor:
        public void __Ctor(Archive.tagUnpacking __formal, void* addr, UInt32 size) => ((delegate* unmanaged[Thiscall]<ref AutoStoreVersionArchive, Archive.tagUnpacking, void*, UInt32, void>)0x005D62C0)(ref this, __formal, addr, size); // .text:005D5170 ; void __thiscall AutoStoreVersionArchive::AutoStoreVersionArchive(AutoStoreVersionArchive *this, Archive::tagUnpacking __formal, void *addr, unsigned int size) .text:005D5170 ??0AutoStoreVersionArchive@@QAE@W4tagUnpacking@Archive@@PAXI@Z

        // AutoStoreVersionArchive.__Ctor:
        public void __Ctor() => ((delegate* unmanaged[Thiscall]<ref AutoStoreVersionArchive, void>)0x0044CE60)(ref this); // .text:0044CCA0 ; void __thiscall AutoStoreVersionArchive::AutoStoreVersionArchive(AutoStoreVersionArchive *this) .text:0044CCA0 ??0AutoStoreVersionArchive@@QAE@XZ

        // AutoStoreVersionArchive.InitForPacking:
        // public void InitForPacking(ArchiveInitializer* i_rInitializer, SmartBuffer* i_buffer) => ((delegate* unmanaged[Thiscall]<ref AutoStoreVersionArchive, ArchiveInitializer*, SmartBuffer*, void>)0xDEADBEEF)(ref this, i_rInitializer, i_buffer); // .text:00446570 ; void __thiscall AutoStoreVersionArchive::InitForPacking(AutoStoreVersionArchive *this, ArchiveInitializer *i_rInitializer, SmartBuffer *i_buffer) .text:00446570 ?InitForPacking@AutoStoreVersionArchive@@MAEXABVArchiveInitializer@@ABVSmartBuffer@@@Z

        // AutoStoreVersionArchive.InitForPacking:
        // public void InitForPacking(ArchiveVersionRow* i_rowInitialData, SmartBuffer* i_buffer) => ((delegate* unmanaged[Thiscall]<ref AutoStoreVersionArchive, ArchiveVersionRow*, SmartBuffer*, void>)0xDEADBEEF)(ref this, i_rowInitialData, i_buffer); // .text:00446700 ; void __thiscall AutoStoreVersionArchive::InitForPacking(AutoStoreVersionArchive *this, ArchiveVersionRow *i_rowInitialData, SmartBuffer *i_buffer) .text:00446700 ?InitForPacking@AutoStoreVersionArchive@@UAEXABVArchiveVersionRow@@ABVSmartBuffer@@@Z

        // AutoStoreVersionArchive.InitForPacking:
        // public void InitForPacking(SmartBuffer* i_buffer) => ((delegate* unmanaged[Thiscall]<ref AutoStoreVersionArchive, SmartBuffer*, void>)0xDEADBEEF)(ref this, i_buffer); // .text:00446590 ; void __thiscall AutoStoreVersionArchive::InitForPacking(AutoStoreVersionArchive *this, SmartBuffer *i_buffer) .text:00446590 ?InitForPacking@AutoStoreVersionArchive@@UAEXABVSmartBuffer@@@Z

        // AutoStoreVersionArchive.InitForPacking:
        // public void InitForPacking(UInt32 i_iCoreVersion, SmartBuffer* i_buffer) => ((delegate* unmanaged[Thiscall]<ref AutoStoreVersionArchive, UInt32, SmartBuffer*, void>)0xDEADBEEF)(ref this, i_iCoreVersion, i_buffer); // .text:00446680 ; void __thiscall AutoStoreVersionArchive::InitForPacking(AutoStoreVersionArchive *this, unsigned int i_iCoreVersion, SmartBuffer *i_buffer) .text:00446680 ?InitForPacking@AutoStoreVersionArchive@@UAEXKABVSmartBuffer@@@Z

        // AutoStoreVersionArchive.InitForUnpacking:
        // public void InitForUnpacking(ArchiveInitializer* i_rInitializer, SmartBuffer* i_buffer) => ((delegate* unmanaged[Thiscall]<ref AutoStoreVersionArchive, ArchiveInitializer*, SmartBuffer*, void>)0xDEADBEEF)(ref this, i_rInitializer, i_buffer); // .text:00446580 ; void __thiscall AutoStoreVersionArchive::InitForUnpacking(AutoStoreVersionArchive *this, ArchiveInitializer *i_rInitializer, SmartBuffer *i_buffer) .text:00446580 ?InitForUnpacking@AutoStoreVersionArchive@@MAEXABVArchiveInitializer@@ABVSmartBuffer@@@Z

        // AutoStoreVersionArchive.InitForUnpacking:
        public void InitForUnpacking(SmartBuffer* i_buffer) => ((delegate* unmanaged[Thiscall]<ref AutoStoreVersionArchive, SmartBuffer*, void>)0x00446770)(ref this, i_buffer); // .text:00446610 ; void __thiscall AutoStoreVersionArchive::InitForUnpacking(AutoStoreVersionArchive *this, SmartBuffer *i_buffer) .text:00446610 ?InitForUnpacking@AutoStoreVersionArchive@@UAEXABVSmartBuffer@@@Z

        // AutoStoreVersionArchive.OnSerializingDone:
        // public void OnSerializingDone() => ((delegate* unmanaged[Thiscall]<ref AutoStoreVersionArchive, void>)0xDEADBEEF)(ref this); // .text:0065D850 ; void __thiscall AutoStoreVersionArchive::OnSerializingDone(AutoStoreVersionArchive *this) .text:0065D850 ?OnSerializingDone@AutoStoreVersionArchive@@QAEXXZ
    }




    public unsafe struct ArchiveInitializer {
        // Struct:
        public ArchiveInitializer.Vtbl* vfptr;
        public override string ToString() => $"vfptr:->(ArchiveInitializer.Vtbl*)0x{(int)vfptr:X8}";
        public unsafe struct Vtbl {
            public static delegate* unmanaged[Thiscall]<ArchiveInitializer*, Archive*, Byte> InitializeArchive; // bool (__thiscall *InitializeArchive)(ArchiveInitializer *this, Archive *);
        }
    }
}