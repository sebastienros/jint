/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-8-13.js
 * @description Array.prototype.some doesn't visit expandos
 */


function testcase() { 
 
  var callCnt = 0;
  function callbackfn(val, idx, obj)
  {
    callCnt++;
    return false;
  }

  var arr = [0,1,2,3,4,5,6,7,8,9];
  arr["i"] = 10;
  arr[true] = 11;
  
  if(arr.some(callbackfn) === false && callCnt === 10) 
    return true;
 }
runTestCase(testcase);
