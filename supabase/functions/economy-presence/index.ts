function reply(status:number,body:unknown){return new Response(JSON.stringify(body),{status,headers:{"content-type":"application/json"}});}
Deno.serve(async(req:Request)=>{
 if(req.method!=="POST")return reply(405,{error:"method_not_allowed"});
 const key=req.headers.get("x-economy-key")??"";
 if(!key||key.length>1024)return reply(401,{error:"unauthorized"});
 let body;
 try{
  const reader=req.body?.getReader();if(!reader)return reply(400,{error:"invalid_json"});
  const chunks:Uint8Array[]=[];let size=0;
  while(true){const {done,value}=await reader.read();if(done)break;size+=value.length;if(size>32768){await reader.cancel();return reply(413,{error:"request_too_large"});}chunks.push(value);}
  const bytes=new Uint8Array(size);let offset=0;for(const chunk of chunks){bytes.set(chunk,offset);offset+=chunk.length;}
  body=JSON.parse(new TextDecoder().decode(bytes));
 }catch{return reply(400,{error:"invalid_json"});}
 const {server_id,steam_ids,observed_at}=body??{};
 if(server_id!=="main"||!Array.isArray(steam_ids)||steam_ids.length>1024||steam_ids.some((s:unknown)=>typeof s!=="string"||!/^\d{17,20}$/.test(s))||typeof observed_at!=="string"||!Number.isFinite(Date.parse(observed_at)))return reply(400,{error:"invalid_request"});
 const url=Deno.env.get("SUPABASE_URL"),service=Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");
 if(!url||!service)return reply(503,{error:"service_unavailable"});
 try{
 const res=await fetch(url+"/rest/v1/rpc/ingest_economy_presence_with_key",{method:"POST",headers:{"content-type":"application/json",apikey:service,authorization:"Bearer "+service},body:JSON.stringify({p_server_id:server_id,p_steam_ids:steam_ids,p_observed_at:observed_at,p_supplied_key:key}),signal:AbortSignal.timeout(10000)});
 if(!res.ok){const error=await res.text();for(const [name,status] of [["unauthorized",401],["invalid_request",400]] as const){if(error.includes(name))return reply(status,{error:name});}return reply(500,{error:"ingest_failed"});}
 return reply(200,await res.json());
 }catch{return reply(503,{error:"service_unavailable"});}
});