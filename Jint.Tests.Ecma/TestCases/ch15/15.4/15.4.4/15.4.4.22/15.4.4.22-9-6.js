/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-6.js
 * @description Array.prototype.reduceRight visits deleted element in array after the call when same index is also present in prototype
 */


function testcase() { 
 
  function callbackfn(prevVal, curVal, idx, obj)  
  {
    delete arr[1];
    delete arr[2];
    return prevVal + curVal;    
  }
  Array.prototype[2] = 6;
  var arr = ['1',2,3,4,5];
  var res = arr.reduceRight(callbackfn);
  delete Array.prototype[2];

  if(res === "151" )    //one element deleted
    return true;  
  
 }
runTestCase(testcase);
