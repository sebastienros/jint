/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-3.js
 * @description Array.prototype.forEach - thisArg is Array
 */


function testcase() {
  var res = false;
  var a = new Array();
  a.res = true;
  var result;
  function callbackfn(val, idx, obj)
  {
    result = this.res;
  }

  var arr = [1];
  arr.forEach(callbackfn,a)
  if( result === true)
    return true;    

 }
runTestCase(testcase);
