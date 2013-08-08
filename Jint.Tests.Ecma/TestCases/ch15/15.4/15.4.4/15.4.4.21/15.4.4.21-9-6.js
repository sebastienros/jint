/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-6.js
 * @description Array.prototype.reduce visits deleted element in array after the call when same index is also present in prototype
 */


function testcase() { 
 
  function callbackfn(prevVal, curVal, idx, obj)  
  {
    delete arr[3];
    delete arr[4];
    return prevVal + curVal;    
  }

  Array.prototype[4] = 5;
  var arr = ['1',2,3,4,5];
  var res = arr.reduce(callbackfn);
  delete Array.prototype[4];

  if(res === "1235"  )    //one element acually deleted
    return true;  
  
 }
runTestCase(testcase);
