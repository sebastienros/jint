/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-1.js
 * @description Array.prototype.reduce - callbackfn not called for indexes never been assigned values
 */


function testcase() { 
 
  var callCnt = 0;
  function callbackfn(prevVal, curVal, idx, obj)
  {
    callCnt++;
    return curVal;
  }

  var arr = new Array(10);
  arr[0] = arr[1] = undefined; //explicitly assigning a value
  if( arr.reduce(callbackfn) === undefined && callCnt === 1)
    return true;    
 }
runTestCase(testcase);
