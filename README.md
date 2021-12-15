# GPU→CPUの同期通信、非同期通信の速度比較  
![image](https://user-images.githubusercontent.com/44022497/146126954-52b2fcbb-3260-4144-bbbc-73af297aa595.jpg)  
Unity 2019.1くらい  
本来https://github.com/toropippi/GPUInstanceShadowsで影付きGPUInstanceの実験をするためのコードだった  
その後ある事情でGetDataとAsyncGPUReadback.Requestを使ったコードでGPU→CPU転送の同期、非同期の影響がフレームレートにどう影響するか気になり検証するために作った。  
  
## 結果  
![gaiyou](https://user-images.githubusercontent.com/44022497/146126957-6ac5e169-46dd-429f-b4c4-25fd65d0a600.png)  
①同期GetData前つき27 .3fps  
②非同期29 .2fps  
③同期GetData後ろつき27 .3fps  
④通信なし29 .5fps  
  
## 結論  
AsyncGPUReadback.Requestで7%程度高速化  
  
### ベースのGPU負荷  
GPU計算75%、レンダリング25%  
使用GPUはRTX2060  
![gpuload](https://user-images.githubusercontent.com/44022497/146126950-f6f75b6b-cefe-4f3f-aaaa-3a1af67a4436.png)  
  
