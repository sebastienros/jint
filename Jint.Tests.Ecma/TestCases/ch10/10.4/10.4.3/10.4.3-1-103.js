/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.4/10.4.3/10.4.3-1-103.js
 * @description Non strict mode should ToObject thisArg if not an object.  Abstract equality operator should succeed.
 */

function testcase(){
  Object.defineProperty(Object.prototype, "x", { get: function () { return this; } }); 
  if((5).x == 0) return false;
  if(!((5).x == 5)) return false;
  return true;
}

runTestCase(testcase);
