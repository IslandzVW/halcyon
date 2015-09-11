import sys

class Edge:
    def __init__(self, (v1, v2), tri, verts):
        self.v1 = v1;
        self.v2 = v2;
        self.tris = [tri]
        self.vertIdx = verts
        
    def append(self, tri):
        self.tris.append(tri)

    def __str__(self):
        return "Edge: " + str((self.v1, self.v2)) + " [" + str((self.vertIdx[self.v1], self.vertIdx[self.v2])) + "]" + "\n Tris: " + str(self.tris) 

        

def describeTri(tri, verts):
    return str(tri) + "{" + str((verts[tri[0]], verts[tri[1]], verts[tri[2]])) + "}"

def describeBuiltTri(tri):
    return str(tri)

def extractAllPossibleVertexCombinations(builtTri):
    combos = []
    combos.append((builtTri[0], builtTri[1], builtTri[2]))
    combos.append((builtTri[0], builtTri[2], builtTri[1]))
    combos.append((builtTri[1], builtTri[0], builtTri[2]))
    combos.append((builtTri[1], builtTri[2], builtTri[0]))
    combos.append((builtTri[2], builtTri[0], builtTri[1]))
    combos.append((builtTri[2], builtTri[1], builtTri[0]))

    return combos




print 'Opening: ' + sys.argv[1]

file = open(sys.argv[1], 'r')

header = file.readline().strip('\n')
if (header != 'OFF'):
    exit('Bad header: ' + header)

counts = file.readline().strip('\n')
counts = counts.split()

vertCount = counts[0]
triCount = counts[1]

print ''
print 'Verts: ' + vertCount + ' Tris: ' + triCount

vertCount = int(vertCount);
triCount = int(triCount);

verts = []
for i in range(vertCount):
    verts.append(tuple(map(float, file.readline().strip('\n').split())))


tris = []
for i in range(triCount):
    tris.append(tuple(map(int, file.readline().strip('\n').split()[1:])))

builtTris = []
for i in range(triCount):
    tri = tris[i]
    builtTris.append(tuple([verts[tri[0]], verts[tri[1]], verts[tri[2]]]))

print ''
print 'Looking for duplicated TRIs by index'
print '------------------------------------'

triWindingIndex = {}
for tri in tris:
    sortedIdx = tuple(sorted(tri))

    if sortedIdx in triWindingIndex:
        firstTri = triWindingIndex[sortedIdx]
        print 'Duplicated TRI detected: '
        print 'First: ' + describeTri(firstTri, verts)
        print 'Duplicate: ' + describeTri(tri, verts)
        print ''
    else:
        triWindingIndex[sortedIdx] = tri

triWindingIndex = None


vertWindingIndex = {}

print ''
print 'Looking for duplicated TRIs by verts'
print '------------------------------------'

for tri in builtTris:
    #create an index with every combination of
    #vertex orders possible for the tri to find
    #the tri regardless of winding
    allCombinations = extractAllPossibleVertexCombinations(tri)

    found = False
    for combo in allCombinations:
        if combo in vertWindingIndex:
            found = True

            firstTri = vertWindingIndex[combo]
            
            print 'Duplicated TRI detected: '
            print 'First: ' + describeBuiltTri(firstTri)
            print 'Duplicate: ' + describeBuiltTri(tri)
            print ''
            
            break

    if not found:
        for combo in allCombinations:
            vertWindingIndex[combo] = tri



print ''
print 'Building edge list...'
print ''


edgeMap = {}
allEdges = []

for tri in tris:
    #index both windings
    e1 = (tri[0], tri[1])
    e2 = (tri[1], tri[2])
    e3 = (tri[2], tri[0])

    fe1 = (tri[0], tri[2])
    fe2 = (tri[2], tri[1])
    fe3 = (tri[1], tri[0])
    
    if not e1 in edgeMap:
        edge = Edge(e1, tri, verts)
        allEdges.append(edge)
        edgeMap[e1] = edge
        edgeMap[fe3] = edge
    else:
        edgeMap[e1].append(tri)
        
    if not e2 in edgeMap:
        edge = Edge(e2, tri, verts)
        allEdges.append(edge)
        edgeMap[e2] = edge
        edgeMap[fe2] = edge
    else:
        edgeMap[e2].append(tri)
        
    if not e3 in edgeMap:
        edge = Edge(e3, tri, verts)
        allEdges.append(edge)
        edgeMap[e3] = edge
        edgeMap[fe1] = edge
    else:
        edgeMap[e3].append(tri)

        

print ''
print 'Checking for open edges'
print '------------------------------------'

for edge in allEdges:
    if len(edge.tris) < 2:
        print "Open " + str(edge)


print ''
print 'Checking non- manifold edges'
print '------------------------------------'

for edge in allEdges:
    if len(edge.tris) > 2:
        print "Non-manifold " + str(edge)
