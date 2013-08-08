/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-3.js
 * @description Array.prototype.every doesn't visit deleted elements in array after the call
 */


function testcase() { 
 
  function callbackfn(val, Idx, obj)
  {
    delete arr[2];
    if(val == 3)
       return false;
    else 
       return true;
  }

  var arr = [1,2,3,4,5];
  
  if(arr.every(callbackfn) === true)    
      return true;  
  
 }
runTestCase(testcase);
