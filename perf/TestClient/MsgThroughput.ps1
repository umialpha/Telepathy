1..10 | %{.\TestClient.exe -h localhost -m 15 -min 1 -n 100000 -r 0 -i 0 -batch 8 -sleep 20000 -responsehandler -nochart -save "MsgThroughput"}