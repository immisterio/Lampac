export async function onRequest(context) {
  try {
    var reqHeaders = new Headers(context.request.headers);
    var response = await context.next();
  
    response.headers.set('Access-Control-Allow-Origin', reqHeaders.get('Origin') || reqHeaders.get('Referer') || "*");
    response.headers.set('Access-Control-Allow-Credentials', 'true');
    response.headers.set('Access-Control-Allow-Methods', '*');
    response.headers.set('Access-Control-Allow-Headers', '*');
    return response;
  } 
  catch (err) {
    return new Response(`${err.message}\n${err.stack}`, { status: 500 });
  }
}