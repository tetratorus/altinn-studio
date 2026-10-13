# Cross-service correlation evidence 20261013T013224Z

instance: `50100001/dfb8696e-9768-4f76-94bb-9d67729cef94` attachment data element: `ee213d90-f50c-40ec-a85f-c213630fcfd7`

## Gateway access log (req_id + traceparent per hop, instance id in path)
```
2026-10-13T01:32:23+00:00 host=local.altinn.cloud req_id=51c2713ca27987cf4bd63d8287263a9e traceparent="00-e1e4985e94a923a80c6fbac76e45735a-0690f57eb1ec0469-01" "GET /storage/api/v1/instances/50100001/dfb8696e-9768-4f76-94bb-9d67729cef94/process/authinfo HTTP/1
2026-10-13T01:32:23+00:00 host=local.altinn.cloud req_id=ab31dc5440265d83b2f85053f674d4cd traceparent="00-e1e4985e94a923a80c6fbac76e45735a-5ddfb75a3e738516-01" "GET /storage/api/v1/instances/50100001/dfb8696e-9768-4f76-94bb-9d67729cef94 HTTP/1.1" 200 6134 upst
2026-10-13T01:32:23+00:00 host=local.altinn.cloud req_id=94fdf7a4b58a90c6d563d24665575578 traceparent="00-e1e4985e94a923a80c6fbac76e45735a-3459e39dd1d51b03-01" "PUT /storage/api/v1/instances/50100001/dfb8696e-9768-4f76-94bb-9d67729cef94/readstatus?status=read 
2026-10-13T01:32:24+00:00 host=local.altinn.cloud req_id=ae8776c7e8c71911fd8bbaf0ce126ef1 traceparent="00-335a540916c46d44fcc819f3580b0c62-eb3b397f1f2d5e85-01" "GET /storage/api/v1/instances/50100001/dfb8696e-9768-4f76-94bb-9d67729cef94/process/authinfo HTTP/1
2026-10-13T01:32:24+00:00 host=local.altinn.cloud req_id=437618ba0cf95b9430bc03244f2e95ac traceparent="00-335a540916c46d44fcc819f3580b0c62-addd21999206e744-01" "GET /storage/api/v1/instances/50100001/dfb8696e-9768-4f76-94bb-9d67729cef94 HTTP/1.1" 200 6134 upst
2026-10-13T01:32:24+00:00 host=local.altinn.cloud req_id=26d98b96ccaeebe7d7707137db0d9705 traceparent="00-335a540916c46d44fcc819f3580b0c62-c31c72021228911a-01" "GET /storage/api/v1/instances/50100001/dfb8696e-9768-4f76-94bb-9d67729cef94/data/ee213d90-f50c-40ec
2026-10-13T01:32:24+00:00 host=local.altinn.cloud req_id=f09bb42c343634b15e56d137f2971187 traceparent="00-335a540916c46d44fcc819f3580b0c62-45456f64dd2548f1-01" "PUT /storage/api/v1/instances/50100001/dfb8696e-9768-4f76-94bb-9d67729cef94/readstatus?status=read 
2026-10-13T01:32:24+00:00 host=app.local.altinn.cloud req_id=f30af3408fc8d7032e58a24345a63c76 traceparent="-" "GET /ttd/frontend-test/instances/50100001/dfb8696e-9768-4f76-94bb-9d67729cef94/data/ee213d90-f50c-40ec-a85f-c213630fcfd7 HTTP/1.1" 200 38 upstream=17
2026-10-13T01:32:24+00:00 host=local.altinn.cloud req_id=dc9809bb659ac5eec72c3663315f7f5d traceparent="00-17af1c51f632d7affcded7cf032e6e1f-29c4c34849d51db0-01" "GET /storage/api/v1/instances/50100001/dfb8696e-9768-4f76-94bb-9d67729cef94/process/authinfo HTTP/1
2026-10-13T01:32:24+00:00 host=local.altinn.cloud req_id=41224ea58c00ce3136864a55b7099e18 traceparent="00-17af1c51f632d7affcded7cf032e6e1f-4328d21d3741c35d-01" "GET /storage/api/v1/instances/50100001/dfb8696e-9768-4f76-94bb-9d67729cef94 HTTP/1.1" 200 6134 upst
2026-10-13T01:32:24+00:00 host=local.altinn.cloud req_id=28ef399c466ca6cc0097e253c2395f1a traceparent="00-17af1c51f632d7affcded7cf032e6e1f-4624d68b589e4ae9-01" "GET /storage/api/v1/instances/50100001/dfb8696e-9768-4f76-94bb-9d67729cef94/data/31da4a38-9bcc-4f9f
2026-10-13T01:32:24+00:00 host=local.altinn.cloud req_id=4ca9f2b0cbd3f7612d8ba121a3e2d7e4 traceparent="00-17af1c51f632d7affcded7cf032e6e1f-08722936fc2524d0-01" "PUT /storage/api/v1/instances/50100001/dfb8696e-9768-4f76-94bb-9d67729cef94/readstatus?status=read 
2026-10-13T01:32:24+00:00 host=app.local.altinn.cloud req_id=a15e6d7e3a490e1d1a552671657c3123 traceparent="-" "GET /ttd/frontend-test/instances/50100001/dfb8696e-9768-4f76-94bb-9d67729cef94/data/31da4a38-9bcc-4f9f-bb16-00aa00ddae6e HTTP/1.1" 200 4654 upstream=
```

## Distinct trace ids touching the instance at the gateway, with the hosts/upstreams they span
```
      4 335a540916c46d44fcc819f3580b0c62
      4 17af1c51f632d7affcded7cf032e6e1f
      3 e1e4985e94a923a80c6fbac76e45735a
```

## OTel collector export (app + workflow-engine OTLP spans/logs containing the instance id)
```
0 spans
```

## Events: cloud event ids + worker trace_log for the instance
```
b1f589d3-2f4b-403d-92f1-18caa781de2f|app.instance.created|2026-10-13T01:30:41.3038959Z
07e43e58-3c1d-4287-b8d4-7c828bdf2441|app.instance.process.movedTo.PdfTask_Task_1|2026-10-13T01:30:42.6509396Z
8cd51edf-207e-4c9c-90d5-2d37c88e9fe6|app.instance.process.movedTo.Task_1|2026-10-13T01:30:41.3038916Z
d3294150-efb2-44e9-9f58-e086765282f4|app.instance.process.movedTo.Task_2|2026-10-13T01:30:44.0628571Z
71377f8d-e206-4ea8-9e5f-de1ae0b49b86|app.instance.process.movedTo.PdfTask_Task_2|2026-10-13T01:30:46.0452179Z
2c91e83b-dee3-457f-a258-ea3c64376fa8|app.instance.process.movedTo.Task_3|2026-10-13T01:30:48.2537257Z
---
b1f589d3-2f4b-403d-92f1-18caa781de2f|Registered|||2026-10-13 01:30:41.498855+00
8cd51edf-207e-4c9c-90d5-2d37c88e9fe6|Registered|||2026-10-13 01:30:41.502974+00
07e43e58-3c1d-4287-b8d4-7c828bdf2441|Registered|||2026-10-13 01:30:42.661318+00
8cd51edf-207e-4c9c-90d5-2d37c88e9fe6|OutboundQueue||1|2026-10-13 01:30:43.182271+00
b1f589d3-2f4b-403d-92f1-18caa781de2f|OutboundQueue||1|2026-10-13 01:30:43.183815+00
07e43e58-3c1d-4287-b8d4-7c828bdf2441|OutboundQueue||1|2026-10-13 01:30:43.18464+00
8cd51edf-207e-4c9c-90d5-2d37c88e9fe6|WebhookPostResponse|200|1|2026-10-13 01:30:43.378272+00
07e43e58-3c1d-4287-b8d4-7c828bdf2441|WebhookPostResponse|200|1|2026-10-13 01:30:43.378368+00
b1f589d3-2f4b-403d-92f1-18caa781de2f|WebhookPostResponse|200|1|2026-10-13 01:30:43.379005+00
d3294150-efb2-44e9-9f58-e086765282f4|Registered|||2026-10-13 01:30:44.074688+00
d3294150-efb2-44e9-9f58-e086765282f4|OutboundQueue||1|2026-10-13 01:30:44.094581+00
d3294150-efb2-44e9-9f58-e086765282f4|WebhookPostResponse|200|1|2026-10-13 01:30:44.106056+00
71377f8d-e206-4ea8-9e5f-de1ae0b49b86|Registered|||2026-10-13 01:30:46.056889+00
71377f8d-e206-4ea8-9e5f-de1ae0b49b86|OutboundQueue||1|2026-10-13 01:30:46.069803+00
71377f8d-e206-4ea8-9e5f-de1ae0b49b86|WebhookPostResponse|200|1|2026-10-13 01:30:46.08407+00
2c91e83b-dee3-457f-a258-ea3c64376fa8|Registered|||2026-10-13 01:30:48.25978+00
2c91e83b-dee3-457f-a258-ea3c64376fa8|OutboundQueue||1|2026-10-13 01:30:48.289572+00
2c91e83b-dee3-457f-a258-ea3c64376fa8|WebhookPostResponse|200|1|2026-10-13 01:30:48.312605+00
```

## Service logs mentioning the instance (docker compose logs, per service)
```
app: 22 log lines
storage: 0 log lines
events: 0 log lines
authorization: 0 log lines
workflow-engine: 0 log lines
pdf3: 0 log lines
subscriber: 0 log lines
```

### Sample: app log lines with the instance id
```
      Request starting HTTP/1.1 GET http://app.local.altinn.cloud/ttd/frontend-test/instances/50100001/dfb8696e-9768-4f76-94bb-9d67729cef94 - - -
      Start processing HTTP request GET https://local.altinn.cloud/storage/api/v1/instances/50100001/dfb8696e-9768-4f76-94bb-9d67729cef94
      Sending HTTP request GET https://local.altinn.cloud/storage/api/v1/instances/50100001/dfb8696e-9768-4f76-94bb-9d67729cef94
      Start processing HTTP request PUT https://local.altinn.cloud/storage/api/v1/instances/50100001/dfb8696e-9768-4f76-94bb-9d67729cef94/readstatus?*
      Sending HTTP request PUT https://local.altinn.cloud/storage/api/v1/instances/50100001/dfb8696e-9768-4f76-94bb-9d67729cef94/readstatus?*
      Request finished HTTP/1.1 GET http://app.local.altinn.cloud/ttd/frontend-test/instances/50100001/dfb8696e-9768-4f76-94bb-9d67729cef94 - 200 - application/json;+charset=utf-8 1056.0894ms
      Request starting HTTP/1.1 GET http://app.local.altinn.cloud/ttd/frontend-test/instances/50100001/dfb8696e-9768-4f76-94bb-9d67729cef94/data/ee213d90-f50c-40ec-a85f-c213630fcfd7 - - -
      Start processing HTTP request GET https://local.altinn.cloud/storage/api/v1/instances/50100001/dfb8696e-9768-4f76-94bb-9d67729cef94
```

### Sample: workflow-engine log lines with the instance id
```
```

### Sample: pdf3 render log
```
time=2026-10-13T01:31:45.060Z level=INFO msg="Browser args" component=browser id=-1 args="[\n  \"--disable-background-networking\",\n  \"--disable-background-timer-throttling\",\n  \"--disable-backgrounding-occluded-windows\",\n  \"--disable-breakpad\",\n  \"--disable-client-side-phishing-detection\
```

### Subscriber deliveries (CloudEvent id + traceparent as received)
```
07e43e58-3c1d-4287-b8d4-7c828bdf2441 app.instance.process.movedTo.PdfTask_Task_1 00-6ce622be53e38c199538480951f6cc76-0f39627ba1bf790e-01
b1f589d3-2f4b-403d-92f1-18caa781de2f app.instance.created 00-25ed848d2b2ef165eb925a5f0bd0a067-3d68e10d6751d797-01
8cd51edf-207e-4c9c-90d5-2d37c88e9fe6 app.instance.process.movedTo.Task_1 00-0a00d3476357b4a9dc86ff2d9288f68d-fb33e8880377ed69-01
d3294150-efb2-44e9-9f58-e086765282f4 app.instance.process.movedTo.Task_2 00-7888820715aa9b5977391bf3d7be41f4-50f3cd975145bc91-01
71377f8d-e206-4ea8-9e5f-de1ae0b49b86 app.instance.process.movedTo.PdfTask_Task_2 00-d6b020c6256a22aa36f55806cf9b3ea5-3eca93984271154b-01
2c91e83b-dee3-457f-a258-ea3c64376fa8 app.instance.process.movedTo.Task_3 00-780e4cb13e7b6c153bbad89adee7543d-1fabf45e577fce94-01
```
