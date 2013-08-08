/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 *
 * @path ch10/10.4/10.4.3/10.4.3-1-104.js
 * @onlyStrict
 * @description Strict mode should not ToObject thisArg if not an object.  Strict equality operator should succeed.
 */
 
 
function testcase(){
  Object.defineProperty(Object.prototype, "x", { get: function () { "use strict"; return this; } }); 
  if(!((5).x === 5)) return false;
  return true;
}

runTestCase(testcase);
