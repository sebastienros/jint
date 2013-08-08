/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * Created based on feedback in https://bugs.ecmascript.org/show_bug.cgi?id=333 
 *
 * @path ch10/10.4/10.4.3/10.4.3-1-106.js
 * @onlyStrict
 * @description Strict mode should not ToObject thisArg if not an object.  Return type should be 'number'.
 */
 
 function testcase(){
  Object.defineProperty(Object.prototype, "x", { get: function () { "use strict"; return this; } }); 
  if(!(typeof (5).x === "number")) return false;
  return true;
}

runTestCase(testcase);
