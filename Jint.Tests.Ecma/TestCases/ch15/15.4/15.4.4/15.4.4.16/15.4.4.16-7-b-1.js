/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-b-1.js
 * @description Array.prototype.every - callbackfn not called for indexes never been assigned values
 */


function testcase() { 
 
  var callCnt = 0.;
  function callbackfn(val, Idx, obj)
  {
    callCnt++;
    return true;
  }

  var arr = new Array(10);
  arr[1] = undefined;  
  arr.every(callbackfn);
  if( callCnt === 1)    
      return true;  
 }
runTestCase(testcase);
