/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-4-5.js
 * @description Array.prototype.reduceRight throws TypeError if callbackfn is number
 */


function testcase() {

  var arr = new Array(10);
  try {
    arr.reduceRight(5);    
  }
  catch(e) {
    if(e instanceof TypeError)
      return true;  
  }

 }
runTestCase(testcase);
