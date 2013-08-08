/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-b-1.js
 * @description Array.prototype.reduceRight returns initialvalue when Array is empty and initialValue is not present
 */


function testcase() { 
 
  function callbackfn(prevVal, curVal, idx, obj)
  {
  }

  var arr = new Array(10);
  
  if(arr.reduceRight(callbackfn,5) === 5)
    return true;  
 }
runTestCase(testcase);
