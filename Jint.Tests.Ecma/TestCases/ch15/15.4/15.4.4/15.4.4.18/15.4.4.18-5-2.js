/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-2.js
 * @description Array.prototype.forEach - thisArg is Object
 */


function testcase() {
  var res = false;
  var o = new Object();
  o.res = true;
  var result;
  function callbackfn(val, idx, obj)
  {
    result = this.res;
  }

  var arr = [1];
  arr.forEach(callbackfn,o)
  if( result === true)
    return true;    

 }
runTestCase(testcase);
