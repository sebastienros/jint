/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-3.js
 * @description Array.prototype.filter doesn't visit deleted elements in array after the call
 */


function testcase() { 
 
  function callbackfn(val, idx, obj)
  {
    delete srcArr[2];
    delete srcArr[4];
    if(val > 0)
      return true;
    else
      return false;
   }

  var srcArr = [1,2,3,4,5];
  var resArr = srcArr.filter(callbackfn);
  if(resArr.length === 3 && resArr[0] === 1 && resArr[2] === 4 )    // two elements deleted
      return true;  
  
 }
runTestCase(testcase);
