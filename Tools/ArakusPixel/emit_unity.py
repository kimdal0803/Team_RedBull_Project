"""copy sheets into the Unity project with pre-sliced .meta files"""
import os, json, hashlib, shutil, struct
from PIL import Image
ROOT=os.path.dirname(os.path.abspath(__file__))
REPO=os.environ.get('UNITY_REPO', os.path.abspath(os.path.join(ROOT,'..','..')))
DST='Assets/MyAssets/Arakus/Sprites'
meta=json.load(open(os.path.join(ROOT,'out','sheets.json')))
FW,FH=meta['frameWidth'],meta['frameHeight']
pivx,pivy=meta['pivot']
def guid(s): return hashlib.md5(('arakus-pixel/'+s).encode()).hexdigest()
def iid(s):
    v=struct.unpack('<q',hashlib.sha1(('arakus-iid/'+s).encode()).digest()[:8])[0]
    return v if v!=0 else 1
def folder_meta(path):
    p=os.path.join(REPO,path+'.meta')
    if os.path.exists(p): return
    open(p,'w').write(f"fileFormatVersion: 2\nguid: {guid(path)}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
PLAT="""  - serializedVersion: 4
    buildTarget: {t}
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
"""
def tex_meta(path, sprites, single_pivot=None):
    """sprites: list of (name, x, y, w, h, pivx, pivy). single_pivot -> spriteMode 1"""
    multiple = single_pivot is None
    lines=[]
    lines.append("fileFormatVersion: 2")
    lines.append(f"guid: {guid(path)}")
    lines.append("TextureImporter:")
    if multiple:
        lines.append("  internalIDToNameTable:")
        for s in sprites:
            lines += ["  - first:", f"      213: {iid(path+'/'+s[0])}", f"    second: {s[0]}"]
    else:
        lines.append("  internalIDToNameTable: []")
    lines.append("""  externalObjects: {}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 1
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50""")
    lines.append(f"  spriteMode: {2 if multiple else 1}")
    lines.append("  spriteExtrude: 1\n  spriteMeshType: 0")
    if multiple:
        lines.append("  alignment: 0\n  spritePivot: {x: 0.5, y: 0.5}")
    else:
        lines.append(f"  alignment: 0\n  spritePivot: {{x: {single_pivot[0]}, y: {single_pivot[1]}}}")
    lines.append("""  spritePixelsToUnits: 45
  spriteBorder: {x: 0, y: 0, z: 0, w: 0}
  spriteGenerateFallbackPhysicsShape: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 1
  swizzle: 50462976
  cookieLightType: 1
  platformSettings:""")
    for t in ['DefaultTexturePlatform','Standalone','WebGL']:
        lines.append(PLAT.format(t=t).rstrip('\n'))
    lines.append("  spriteSheet:\n    serializedVersion: 2")
    if multiple:
        lines.append("    sprites:")
        for (name,x,y,w,h,px,py) in sprites:
            lines.append(f"""    - serializedVersion: 2
      name: {name}
      rect:
        serializedVersion: 2
        x: {x}
        y: {y}
        width: {w}
        height: {h}
      alignment: 9
      pivot: {{x: {px}, y: {py}}}
      border: {{x: 0, y: 0, z: 0, w: 0}}
      customData: 
      outline: []
      physicsShape: []
      tessellationDetail: 0
      bones: []
      spriteID: {hashlib.md5(('sid/'+path+'/'+name).encode()).hexdigest()}
      internalID: {iid(path+'/'+name)}
      vertices: []
      indices: 
      edges: []
      weights: []""")
    else:
        lines.append("    sprites: []")
    lines.append(f"""    outline: []
    customData: 
    physicsShape: []
    bones: []
    spriteID: {hashlib.md5(('main/'+path).encode()).hexdigest()}
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []""")
    if multiple:
        lines.append("    nameFileIdTable:")
        for s in sprites:
            lines.append(f"      {s[0]}: {iid(path+'/'+s[0])}")
    else:
        lines.append("    nameFileIdTable: {}")
    lines.append("""  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: """)
    open(os.path.join(REPO,path+'.meta'),'w').write('\n'.join(lines)+'\n')

for d in ['Assets/MyAssets/Arakus','Assets/MyAssets/Arakus/Sprites']:
    folder_meta(d)
index={}
for name,info in meta['sheets'].items():
    src=os.path.join(ROOT,'out','sheets',name+'.png')
    path=f'{DST}/{name}.png'
    shutil.copy(src,os.path.join(REPO,path))
    n=info['frames']
    sprites=[(f'{name}_{i}', i*FW, 0, FW, FH, pivx, pivy) for i in range(n)]
    tex_meta(path,sprites)
    index[name]={'guid':guid(path),'sprites':[(s[0],iid(path+'/'+s[0])) for s in sprites]}
# shield projectile: single sprite, centered pivot
path=f'{DST}/Arakus_Shield_Projectile.png'
shutil.copy(os.path.join(ROOT,'out','sheets','Arakus_Shield_Projectile.png'),os.path.join(REPO,path))
tex_meta(path,[],single_pivot=(0.5,0.5))
index['Arakus_Shield_Projectile']={'guid':guid(path)}
json.dump(index,open(os.path.join(ROOT,'out','unity_index.json'),'w'),indent=1)
print('wrote',len(index),'textures; pivot',pivx,pivy)
