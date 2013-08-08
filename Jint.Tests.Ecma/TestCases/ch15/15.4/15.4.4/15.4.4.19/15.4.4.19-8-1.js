/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-1.js
 * @description Array.prototype.map doesn't consider new elements added to array after it is called
 */


function testcase() { 
 
  function callbackfn(val, idx, obj)
  {
    srcArr[2] = 3;
    srcArr[5] = 6;
    return 1;
  }

  var srcArr = [1,2,,4,5];
  var resArr = srcArr.map(callbackfn);
  if(resArr.length === 5)
      return true;  
  
 }
runTestCase(testcase);
