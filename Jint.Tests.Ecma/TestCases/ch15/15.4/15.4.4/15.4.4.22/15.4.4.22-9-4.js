/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-4.js
 * @description Array.prototype.reduceRight doesn't consider unvisited deleted elements when Array.length is decreased
 */


function testcase() { 
 
  function callbackfn(prevVal, curVal, idx, obj)
  {
    arr.length = 2;
    return prevVal + curVal;
  }

  var arr = [1,2,3,4,5];
  if(arr.reduceRight(callbackfn) === 12 )
    return true;  
  
 }
runTestCase(testcase);
