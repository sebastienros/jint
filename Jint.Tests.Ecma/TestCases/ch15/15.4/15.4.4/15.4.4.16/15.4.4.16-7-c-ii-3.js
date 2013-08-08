/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-7-c-ii-3.js
 * @description Array.prototype.every immediately returns false if callbackfn returns false
 */


function testcase() { 
 
  var callCnt = 0;
  function callbackfn(val, idx, obj)
  {
    callCnt++;
    if(idx > 5)   
       return false;
    else
       return true;
  }

  var arr = [0,1,2,3,4,5,6,7,8,9];
  
  if(arr.every(callbackfn) === false && callCnt === 7) 
    return true;


 }
runTestCase(testcase);
