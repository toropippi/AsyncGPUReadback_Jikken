# GPU→CPUの同期通信、非同期通信の速度比較

Unity 2019.1くらい  
本来https://github.com/toropippi/GPUInstanceShadowsで影付きGPUInstanceの実験をするためのコードだった  
その後ある事情でGetDataとAsyncGPUReadback.Requestを使ったコードでGPU→CPU転送の同期、非同期の影響がフレームレートにどう影響するか気になり検証するために作った。  
  
## 結果  
①同期GetData前つき27 .3fps  
②非同期29 .2fps  
③同期GetData後ろつき27 .3fps  
④通信なし29 .5fps  
  
## 結論  
AsyncGPUReadback.Requestで7%程度高速化  
  
### ベースのGPU負荷  
GPU計算75%、レンダリング25%  
使用GPUはRTX2060  
  
