/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-4.js
 * @description Array.prototype.every doesn't visit deleted elements when Array.length is decreased
 */


function testcase() { 
 
  function callbackfn(val, Idx, obj)
  {
    arr.length = 3;
    if(val < 4)
       return true;
    else 
       return false;
  }

  var arr = [1,2,3,4,6];
  
  if(arr.every(callbackfn) === true)    
      return true;  
  
 }
runTestCase(testcase);
