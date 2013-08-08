/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-5.js
 * @description Array.prototype.reduceRight - callbackfn not called for array with one element
 */


function testcase() { 
 
  var callCnt = 0;
  function callbackfn(prevVal, curVal, idx, obj)
  {
    callCnt++;
    return 2;
  }

  var arr = [1];
  if(arr.reduceRight(callbackfn) === 1 && callCnt === 0 )
    return true;    
 }
runTestCase(testcase);
