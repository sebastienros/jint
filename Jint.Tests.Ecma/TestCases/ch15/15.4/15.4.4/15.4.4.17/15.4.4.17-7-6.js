/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-6.js
 * @description Array.prototype.some visits deleted element in array after the call when same index is also present in prototype
 */


function testcase() { 
 
  function callbackfn(val, idx, obj)
  {
    delete arr[4];
    if(val < 5)
      return false;
    else 
      return true;
  }


  Array.prototype[4] = 5;
  var arr = [1,2,3,4,5];
  
  var res = arr.some(callbackfn) ;
  delete Array.prototype[4];
  if(res === true)    
    return true;  
  
 }
runTestCase(testcase);
