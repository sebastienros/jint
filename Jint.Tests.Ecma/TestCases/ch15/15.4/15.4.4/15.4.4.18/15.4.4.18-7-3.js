/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-3.js
 * @description Array.prototype.forEach doesn't visit deleted elements when Array.length is decreased
 */


function testcase() { 
 
  var callCnt = 0;
  function callbackfn(val, idx, obj)
  {
    arr.length=3;
    callCnt++;
  }

  var arr = [1,2,3,4,5];
  arr.forEach(callbackfn);
  if( callCnt === 3)    
      return true;  
  
 }
runTestCase(testcase);
