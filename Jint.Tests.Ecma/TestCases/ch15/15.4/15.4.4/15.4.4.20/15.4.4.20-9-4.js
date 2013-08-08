/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-4.js
 * @description Array.prototype.filter doesn't visit deleted elements when Array.length is decreased
 */


function testcase() { 
 
  function callbackfn(val, idx, obj)
  {
    srcArr.length = 2;
    return true;
  }

  var srcArr = [1,2,3,4,6];
  var resArr = srcArr.filter(callbackfn);
  if(resArr.length === 2 )
      return true;  
  
 }
runTestCase(testcase);
