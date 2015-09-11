
extern "C" __declspec(dllexport) void* __stdcall Decompose(float verts[], int indicies[], int vertCount, int indexCount,
	float ccConnectDist, int nClusters, float concavity, int targetNTrianglesDecimatedMesh, int maxVertsPerCH,
	bool addExtraDistPoints, bool addFacesPoints, float volumeWeight, float smallClusterThreshold);

extern "C" __declspec(dllexport) int __stdcall GetConvexHullCount(void* session);

extern "C" __declspec(dllexport) int __stdcall GetVertexCount(void* session, int convexIndex);

extern "C" __declspec(dllexport) int __stdcall GetIndexCount(void* session, int convexIndex);

extern "C" __declspec(dllexport) bool __stdcall GetConvexVertsAndIndexes(void* session, int convexIndex, float* verts, int* indexes);

extern "C" __declspec(dllexport) bool __stdcall FreeSession(void* session);