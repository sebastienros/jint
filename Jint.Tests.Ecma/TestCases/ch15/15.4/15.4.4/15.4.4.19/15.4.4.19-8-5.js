/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-5.js
 * @description Array.prototype.map doesn't consider newly added elements in sparse array
 */


function testcase() { 
 
  var callCnt = 0;
  function callbackfn(val, idx, obj)
  {
    srcArr[1000] = 3;
    callCnt++;
    return val;
  }

  var srcArr = new Array(10);
  srcArr[1] = 1;
  srcArr[2] = 2;
  var resArr = srcArr.map(callbackfn);
  if( resArr.length === 10 && callCnt === 2)    
      return true;  
  
 }
runTestCase(testcase);
