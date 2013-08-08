/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-6.js
 * @description Array.prototype.map visits deleted element in array after the call when same index is also present in prototype
 */


function testcase() { 
 
  function callbackfn(val, idx, obj)
  {
    delete srcArr[4];
    if(val > 0)
      return 1;
    else
      return 0;

  }

  Array.prototype[4] = 5;
  var srcArr = [1,2,3,4,5];
  var resArr = srcArr.map(callbackfn);
  delete Array.prototype[4];
  if(resArr.length === 5 && resArr[4] === 1)
    return true;  
  
 }
runTestCase(testcase);
