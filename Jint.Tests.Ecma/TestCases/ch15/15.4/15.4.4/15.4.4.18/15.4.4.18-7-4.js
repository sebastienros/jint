/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-7-4.js
 * @description Array.prototype.forEach doesn't consider newly added elements in sparse array
 */


function testcase() { 
 
  var callCnt = 0;
  function callbackfn(val, idx, obj)
  {
    arr[1000] = 3;
    callCnt++;
  }

  var arr = new Array(10);
  arr[1] = 1;
  arr[2] = 2;
  arr.forEach(callbackfn);
  if( callCnt === 2)    
      return true;  
  
 }
runTestCase(testcase);
