if exist certs-test (
    rmdir certs-test /q /s
) 
mkdir certs-test && cd certs-test

openssl genrsa -passout pass:1234 -des3 -out ca.key 4096

openssl req -passin pass:1234 -new -x509 -days 365 -key ca.key -out ca.crt -subj  "/C=CN/ST=SH/L=SH/O=Test/OU=Test/CN=ca"

openssl genrsa -passout pass:1234 -des3 -out server.key 4096

openssl req -passin pass:1234 -new -key server.key -out server.csr -config ../openssl.cnf  -subj "/C=CN/ST=SH/L=SH/O=Test/OU=Server/CN=localhost"

openssl x509 -req -passin pass:1234 -days 365 -in server.csr -CA ca.crt -CAkey ca.key -set_serial 01 -out server.crt -extensions v3_req -extfile ../openssl.cnf

openssl rsa -passin pass:1234 -in server.key -out server.key

openssl pkcs12 -export -password pass:1234 -out server.pfx -inkey server.key -in server.crt

openssl genrsa -passout pass:1234 -des3 -out client.key 4096

openssl req -passin pass:1234 -new -key client.key -out client.csr -config ../openssl.cnf -subj  "/C=CN/ST=SH/L=SH/O=Test/OU=Client/CN=localhost"

openssl x509 -passin pass:1234 -req -days 365 -in client.csr -CA ca.crt -CAkey ca.key -set_serial 01 -out client.crt -extensions v3_req -extfile ../openssl.cnf

openssl rsa -passin pass:1234 -in client.key -out client.key

openssl pkcs12 -export -password pass:1234 -out client.pfx -inkey client.key -in client.crt

xcopy /s/Y server.pfx ..\BrokerServer 
xcopy /s/Y client.pfx ..\BrokerServer
xcopy /s/Y server.pfx ..\WorkerServer
xcopy /s/Y client.pfx ..\BrokerClient