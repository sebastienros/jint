/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-2.js
 * @description Array.prototype.map - thisArg is Object
 */


function testcase() {
  var res = false;
  var o = new Object();
  o.res = true;
  function callbackfn(val, idx, obj)
  {
    return this.res;
  }

  var srcArr = [1];
  var resArr = srcArr.map(callbackfn,o);
  if( resArr[0] === true)
    return true;    

 }
runTestCase(testcase);
