/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-8.js
 * @description Array.prototype.indexOf must return correct index (Array)
 */


function testcase() {
  var b = new Array("0,1");  
  var a = new Array(0,b,"0,1",3);  
  if (a.indexOf(b.toString()) === 2 &&  
      a.indexOf("0,1") === 2 ) 
  {
    return true;
  }
 }
runTestCase(testcase);
