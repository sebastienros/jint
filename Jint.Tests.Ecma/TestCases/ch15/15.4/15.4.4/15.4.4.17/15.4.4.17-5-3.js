/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-3.js
 * @description Array.prototype.some - thisArg is Array
 */


function testcase() {
  var res = false;
  var a = new Array();
  a.res = true;
  function callbackfn(val, idx, obj)
  {
    return this.res;
  }

  var arr = [1];

  if(arr.some(callbackfn, a) === true)
    return true;    

 }
runTestCase(testcase);
