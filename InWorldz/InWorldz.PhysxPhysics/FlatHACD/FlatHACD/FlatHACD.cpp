// FlatHACD.cpp : Defines the exported functions for the DLL application.
//

#include "stdafx.h"
#include "FlatHACD.h"

#include "hacdHACD.h"

#include <map>
#include <iostream>
#include <string>

bool SaveOFF(const std::string & fileName, const std::vector< HACD::Vec3<HACD::Real> > & points, const std::vector< HACD::Vec3<long> > & triangles);
bool SaveOFF(const std::string & fileName, size_t nV, size_t nT, const HACD::Vec3<HACD::Real> * const points, const HACD::Vec3<long> * const triangles);

struct HacdSession
{
	HACD::HeapManager * heapManager;
	HACD::HACD * hacd;
};

void CallBack(const char * msg, double progress, double concavity, size_t nVertices)
{
	std::cout << msg;
}

void* __stdcall Decompose(float verts[], int indicies[], int vertCount, int indexCount,
	float ccConnectDist, int nClusters, float concavity, int targetNTrianglesDecimatedMesh,
	int maxVertsPerCH, bool addExtraDistPoints, bool addFacesPoints, float volumeWeight,
	float smallClusterThreshold)
{
	std::vector< HACD::Vec3<HACD::Real> > points;
	points.reserve(vertCount/3);
	std::vector< HACD::Vec3<long> > triangles;
	triangles.reserve(indexCount/3);

	//fill arrays
	for (int i = 0; i < vertCount; i += 3)
	{
		HACD::Vec3<HACD::Real> pt(verts[i + 0], verts[i + 1], verts[i + 2]);
		points.push_back(pt);
	}

	for (int i = 0; i < indexCount; i += 3)
	{
		HACD::Vec3<long> idx(indicies[i + 0], indicies[i + 1], indicies[i + 2]);
		triangles.push_back(idx);
	}

	//SaveOFF("lastMesh.off", points, triangles);

	HACD::HeapManager * heapManager = HACD::createHeapManager(16384*(1000));//HACD::createHeapManager(65536*(1000));

	HACD::HACD * myHACD = HACD::CreateHACD(heapManager);
	myHACD->SetPoints(&points[0]);
	myHACD->SetNPoints(points.size());
	myHACD->SetTriangles(&triangles[0]);
	myHACD->SetNTriangles(triangles.size());
	myHACD->SetCompacityWeight(0.0001);
    myHACD->SetVolumeWeight(volumeWeight);
    myHACD->SetConnectDist(ccConnectDist);               // if two connected components are seperated by distance < ccConnectDist
                                                        // then create a virtual edge between them so the can be merged during 
                                                        // the simplification process
	      
    myHACD->SetNClusters(nClusters);                     // minimum number of clusters
    myHACD->SetNVerticesPerCH(maxVertsPerCH);                      // max of 100 vertices per convex-hull
	myHACD->SetConcavity(concavity);                     // maximum concavity
	myHACD->SetSmallClusterThreshold(smallClusterThreshold);				 // threshold to detect small clusters
	myHACD->SetNTargetTrianglesDecimatedMesh(targetNTrianglesDecimatedMesh); // # triangles in the decimated mesh
    myHACD->SetAddExtraDistPoints(addExtraDistPoints);   
    myHACD->SetAddFacesPoints(addFacesPoints); 

	//myHACD->SetCallBack(&CallBack);

	if (myHACD->Compute()) {
		HacdSession* session = new HacdSession;
		session->hacd = myHACD;
		session->heapManager = heapManager;

		return session;

	} else {
		HACD::DestroyHACD(myHACD);
		HACD::releaseHeapManager(heapManager);
		
		return 0;
	}

    
	return false;
}

int __stdcall GetConvexHullCount(void* session)
{
	HacdSession* hacdSession = reinterpret_cast<HacdSession*>(session);

	return (int)hacdSession->hacd->GetNClusters();
}

int __stdcall GetVertexCount(void* session, int convexIndex)
{
	HacdSession* hacdSession = reinterpret_cast<HacdSession*>(session);

	return (int)hacdSession->hacd->GetNPointsCH(convexIndex) * 3;
}

int __stdcall GetIndexCount(void* session, int convexIndex)
{
	HacdSession* hacdSession = reinterpret_cast<HacdSession*>(session);

	return (int)hacdSession->hacd->GetNTrianglesCH(convexIndex) * 3;
}

bool __stdcall GetConvexVertsAndIndexes(void* session, int convexIndex, float* verts, int* indexes)
{
	HacdSession* hacdSession = reinterpret_cast<HacdSession*>(session);


	size_t nPoints = hacdSession->hacd->GetNPointsCH(convexIndex);
	size_t nTriangles = hacdSession->hacd->GetNTrianglesCH(convexIndex);

	HACD::Vec3<HACD::Real> * pointsCH = new HACD::Vec3<HACD::Real>[nPoints];
	HACD::Vec3<long> * trianglesCH = new HACD::Vec3<long>[nTriangles];

	if (! hacdSession->hacd->GetCH(convexIndex, pointsCH, trianglesCH)) {
		delete[] pointsCH;
		delete[] trianglesCH;

		return false;
	}

	for (int i = 0, j = 0; i < nPoints; i++, j += 3)
	{
		verts[j + 0] = pointsCH[i].X();
		verts[j + 1] = pointsCH[i].Y();
		verts[j + 2] = pointsCH[i].Z();
	}

	for (int i = 0, j = 0; i < nTriangles; i++, j += 3)
	{
		indexes[j + 0] = (int)trianglesCH[i].X();
		indexes[j + 1] = (int)trianglesCH[i].Y();
		indexes[j + 2] = (int)trianglesCH[i].Z();
	}

	delete[] pointsCH;
	delete[] trianglesCH;

	return true;
}

bool __stdcall FreeSession(void* session)
{
	HacdSession* hacdSession = reinterpret_cast<HacdSession*>(session);

	HACD::DestroyHACD(hacdSession->hacd);
	HACD::releaseHeapManager(hacdSession->heapManager);

	delete hacdSession;

	return true;
}

bool SaveOFF(const std::string & fileName, const std::vector< HACD::Vec3<HACD::Real> > & points, const std::vector< HACD::Vec3<long> > & triangles)
{
	return SaveOFF(fileName,points.size(), triangles.size(), &points[0], &triangles[0]);
}

bool SaveOFF(const std::string & fileName, size_t nV, size_t nT, const HACD::Vec3<HACD::Real> * const points, const HACD::Vec3<long> * const triangles)
{
    std::cout << "Saving " <<  fileName << std::endl;
    std::ofstream fout(fileName.c_str());
    if (fout.is_open()) 
    {           
        fout <<"OFF" << std::endl;	    	
        fout << nV << " " << nT << " " << 0<< std::endl;		
        for(size_t v = 0; v < nV; v++)
        {
            fout << points[v].X() << " " 
                 << points[v].Y() << " " 
                 << points[v].Z() << std::endl;
		}
        for(size_t f = 0; f < nT; f++)
        {
            fout <<"3 " << triangles[f].X() << " " 
                        << triangles[f].Y() << " "                                                  
                        << triangles[f].Z() << std::endl;
        }
        fout.close();
	    return true;
    }
    return false;
}