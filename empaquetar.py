import io, sys, zipfile

# Uso: python3 empaquetar.py base.scmod codigo.dll salida.scmod
# Los .scmod son: 49 bytes de cabecera + un zip con los bytes invertidos.
base, dll, salida = sys.argv[1:4]

datos = open(base, "rb").read()
cabecera = datos[:49]
origen = zipfile.ZipFile(io.BytesIO(datos[49:][::-1]))

buf = io.BytesIO()
with zipfile.ZipFile(buf, "w", zipfile.ZIP_DEFLATED) as z:
    for item in origen.infolist():
        z.writestr(item, origen.read(item))
    zi = zipfile.ZipInfo(dll.replace("\\", "/").split("/")[-1])
    zi.compress_type = zipfile.ZIP_DEFLATED
    zi.flag_bits |= 0x800
    z.writestr(zi, open(dll, "rb").read())

open(salida, "wb").write(cabecera + buf.getvalue()[::-1])

# Comprobacion
final = open(salida, "rb").read()
comprobar = zipfile.ZipFile(io.BytesIO(final[49:][::-1]))
assert comprobar.testzip() is None
print("OK:", salida, comprobar.namelist())
