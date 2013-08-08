/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-2.js
 * @description Array.prototype.some - thisArg is Object
 */


function testcase() {
  var res = false;
  var o = new Object();
  o.res = true;
  function callbackfn(val, idx, obj)
  {
    return this.res;
  }

  var arr = [1];
  if(arr.some(callbackfn, o) === true)
    return true;    

 }
runTestCase(testcase);
